#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
suno_client.py
Cliente directo local para Suno AI (sin intermediarios ni servicios de pago de terceros).
Conecta directamente tu cuenta local con los endpoints de Suno (studio-api.suno.ai)
usando tu cookie de sesión de Clerk o token Bearer.
"""

import sys
import os
import json
import time
import argparse
import re
from pathlib import Path

try:
    import requests
except ImportError:
    print("Error: falta la librería 'requests'. Instálala con: pip install requests")
    sys.exit(1)

# En Windows, asegurar que la salida de consola use UTF-8 para emojis y tildes
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if sys.stderr and hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


CONFIG_FILE = Path(__file__).parent / "suno_config.json"
DEFAULT_OUTPUT_DIR = Path(__file__).parent.parent.parent / "Assets" / "Audio" / "Music"

CLERK_VERSION = "4.73.2"
CLERK_CLIENT_URL = f"https://clerk.suno.com/v1/client?_clerk_js_version={CLERK_VERSION}"
CLERK_SESSION_URL = f"https://clerk.suno.com/v1/client/sessions/{{session_id}}/tokens?_clerk_js_version={CLERK_VERSION}"

STUDIO_API_BASE = "https://studio-api.suno.ai/api"
GENERATE_URL = f"{STUDIO_API_BASE}/generate/v2/"
FEED_URL = f"{STUDIO_API_BASE}/feed/"
BILLING_URL = f"{STUDIO_API_BASE}/billing/info/"

DEFAULT_USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"
)


def clean_filename(name: str) -> str:
    """Sanitiza una cadena para usarla como nombre de archivo en Windows."""
    name = re.sub(r'[\\/*?:"<>|]', "", name)
    name = name.strip().replace(" ", "_")
    return name[:60] if name else "suno_track"


class SunoSession:
    def __init__(self, config_path: Path = CONFIG_FILE):
        self.config_path = config_path
        self.cookie = ""
        self.bearer_token = ""
        self.output_dir = DEFAULT_OUTPUT_DIR
        self._cached_jwt = ""
        self._jwt_expires_at = 0.0
        self.load_config()

    def load_config(self):
        if not self.config_path.exists():
            return
        try:
            with open(self.config_path, "r", encoding="utf-8") as f:
                data = json.load(f)
                self.cookie = data.get("cookie", "").strip()
                self.bearer_token = data.get("bearer_token", "").strip()
                out_dir = data.get("output_dir")
                if out_dir:
                    self.output_dir = Path(out_dir).resolve()
        except Exception as e:
            print(f"[Aviso] No se pudo leer {self.config_path}: {e}")

    def save_config(self):
        data = {
            "cookie": self.cookie,
            "bearer_token": self.bearer_token,
            "output_dir": str(self.output_dir),
        }
        with open(self.config_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)

    def get_jwt(self, force_refresh=False) -> str:
        # 1. Si hay un Bearer token directo estático configurado y no hay cookie, usarlo:
        if self.bearer_token and not self.cookie:
            return self.bearer_token

        # 2. Si tenemos un token en caché válido, reutilizarlo:
        now = time.time()
        if not force_refresh and self._cached_jwt and now < self._jwt_expires_at - 10:
            return self._cached_jwt

        # 3. Si tenemos cookie, refrescar token vía Clerk:
        if not self.cookie:
            if self.bearer_token:
                return self.bearer_token
            raise ValueError(
                f"No hay 'cookie' ni 'bearer_token' configurado en {self.config_path}.\n"
                "Configura tu cookie ejecutando: python suno_client.py configure"
            )

        clerk_headers = {
            "User-Agent": DEFAULT_USER_AGENT,
            "Cookie": self.cookie,
            "Origin": "https://suno.com",
            "Referer": "https://suno.com/",
            "Accept": "application/json",
        }

        # Paso A: Obtener sesión activa de Clerk
        res = requests.get(CLERK_CLIENT_URL, headers=clerk_headers, timeout=15)
        if res.status_code != 200:
            raise RuntimeError(
                f"Error al contactar Clerk ({res.status_code}): {res.text[:200]}\n"
                "Verifica que la cookie de sesión no haya caducado."
            )

        client_data = res.json()
        response_obj = client_data.get("response") or {}
        session_id = response_obj.get("last_active_session_id")

        if not session_id:
            # Intentar buscar en sessions list
            sessions = response_obj.get("sessions") or []
            if sessions:
                session_id = sessions[0].get("id")

        if not session_id:
            raise RuntimeError(
                "Clerk no devolvió una sesión activa. La cookie copiada puede estar incompleta o caducada."
            )

        # Paso B: Generar nuevo JWT para la sesión
        token_url = CLERK_SESSION_URL.format(session_id=session_id)
        token_res = requests.post(token_url, headers=clerk_headers, timeout=15)
        if token_res.status_code != 200:
            raise RuntimeError(f"Error generando JWT en Clerk ({token_res.status_code}): {token_res.text[:200]}")

        token_data = token_res.json()
        jwt = token_data.get("jwt")
        if not jwt:
            raise RuntimeError("Respuesta de Clerk sin JWT.")

        self._cached_jwt = jwt
        self._jwt_expires_at = now + 50.0  # Los tokens de Clerk suelen durar 60 segundos
        return jwt

    def get_api_headers(self) -> dict:
        jwt = self.get_jwt()
        return {
            "User-Agent": DEFAULT_USER_AGENT,
            "Authorization": f"Bearer {jwt}",
            "Accept": "application/json, text/plain, */*",
            "Origin": "https://suno.com",
            "Referer": "https://suno.com/",
            "Content-Type": "application/json",
        }


class SunoClient:
    def __init__(self, session: SunoSession):
        self.session = session

    def check_billing(self) -> dict:
        headers = self.session.get_api_headers()
        res = requests.get(BILLING_URL, headers=headers, timeout=15)
        if res.status_code == 200:
            return res.json()
        else:
            raise RuntimeError(f"Error consultando billing ({res.status_code}): {res.text[:200]}")

    def generate(
        self,
        prompt: str,
        tags: str,
        title: str,
        instrumental: bool = False,
        mv: str = "chirp-v3-5",
    ) -> list:
        """
        Envía la petición de generación a Suno.
        Devuelve la lista de clips creados (normalmente 2 variaciones).
        """
        headers = self.session.get_api_headers()
        payload = {
            "prompt": prompt,
            "tags": tags,
            "title": title,
            "make_instrumental": instrumental,
            "mv": mv,
        }

        res = requests.post(GENERATE_URL, json=payload, headers=headers, timeout=25)
        if res.status_code not in (200, 201):
            raise RuntimeError(f"Error en generación Suno ({res.status_code}): {res.text[:300]}")

        data = res.json()
        clips = data.get("clips") or []
        if not clips and "id" in data:
            clips = [data]
        return clips

    def get_feed(self, clip_ids: list = None) -> list:
        headers = self.session.get_api_headers()
        params = {}
        if clip_ids:
            params["ids"] = ",".join(clip_ids)
        res = requests.get(FEED_URL, headers=headers, params=params, timeout=15)
        if res.status_code == 200:
            return res.json()
        else:
            raise RuntimeError(f"Error consultando feed ({res.status_code}): {res.text[:200]}")

    def wait_for_clips(self, clip_ids: list, timeout_sec: int = 300, poll_sec: int = 5) -> list:
        """Espera a que los clips completen su generación."""
        print(f"⏳ Esperando generación de clips: {clip_ids} (timeout: {timeout_sec}s)...")
        start_time = time.time()
        last_statuses = {}

        while time.time() - start_time < timeout_sec:
            try:
                clips = self.get_feed(clip_ids)
            except Exception as e:
                print(f"  [Aviso al consultar feed]: {e}")
                time.sleep(poll_sec)
                continue

            all_done = True
            current_status_summary = []

            for clip in clips:
                cid = clip.get("id")
                st = clip.get("status", "unknown")
                last_statuses[cid] = st
                current_status_summary.append(f"{cid[:8]}: {st}")
                if st not in ("complete", "error"):
                    all_done = False

            elapsed = int(time.time() - start_time)
            print(f"  [{elapsed}s] Estado: {' | '.join(current_status_summary)}")

            if all_done and len(clips) >= len(clip_ids):
                return clips

            time.sleep(poll_sec)

        print("⚠️ Tiempo de espera agotado antes de completar todos los clips.")
        return self.get_feed(clip_ids)

    def download_clip(self, clip: dict, target_dir: Path = None) -> Path:
        """Descarga el MP3 de un clip al directorio destino."""
        if target_dir is None:
            target_dir = self.session.output_dir
        target_dir.mkdir(parents=True, exist_ok=True)

        audio_url = clip.get("audio_url")
        if not audio_url:
            raise ValueError(f"El clip {clip.get('id')} no tiene audio_url disponible.")

        title = clip.get("title") or "suno_song"
        safe_title = clean_filename(title)
        clip_id_short = clip.get("id", "")[:8]
        dest_file = target_dir / f"{safe_title}_{clip_id_short}.mp3"

        print(f"⬇️ Descargando audio desde: {audio_url}")
        res = requests.get(audio_url, stream=True, timeout=30)
        res.raise_for_status()

        with open(dest_file, "wb") as f:
            for chunk in res.iter_content(chunk_size=65536):
                if chunk:
                    f.write(chunk)

        # Guardar metadata complementaria
        meta_file = target_dir / f"{safe_title}_{clip_id_short}.json"
        with open(meta_file, "w", encoding="utf-8") as f:
            json.dump(clip, f, indent=2, ensure_ascii=False)

        print(f"✅ Guardado en: {dest_file}")
        return dest_file


def cmd_configure(args, session: SunoSession):
    print("=== Configuración de Suno Client ===")
    print("Pega aquí tu Cookie completa obtenida de suno.com (Inspeccionar -> Red -> Cookie):")
    cookie_input = input("Cookie: ").strip()
    if cookie_input:
        session.cookie = cookie_input

    print("\n¿Quieres especificar un Bearer token directo? (Opcional, presiona Enter para omitir):")
    bearer_input = input("Bearer Token: ").strip()
    if bearer_input:
        if bearer_input.lower().startswith("bearer "):
            bearer_input = bearer_input[7:].strip()
        session.bearer_token = bearer_input

    session.save_config()
    print(f"\nConfiguración guardada en: {session.config_path}")
    print("Probando conexión...")
    try:
        client = SunoClient(session)
        info = client.check_billing()
        credits_left = info.get("total_credits_left", "Desconocido")
        print(f"🎉 ¡Conexión exitosa! Créditos disponibles en tu cuenta: {credits_left}")
    except Exception as e:
        print(f"❌ Error al verificar conexión: {e}")


def cmd_check(args, session: SunoSession):
    try:
        client = SunoClient(session)
        info = client.check_billing()
        credits_left = info.get("total_credits_left")
        period = info.get("period")
        monthly_limit = info.get("monthly_limit")
        print(f"✅ Conexión con Suno activa.")
        print(f"   - Créditos restantes: {credits_left}")
        if monthly_limit:
            print(f"   - Límite mensual: {monthly_limit}")
        if period:
            print(f"   - Período: {period}")
    except Exception as e:
        print(f"❌ Error de conexión con Suno: {e}")
        sys.exit(1)


def cmd_generate(args, session: SunoSession):
    prompt_text = args.prompt
    if args.prompt_file:
        with open(args.prompt_file, "r", encoding="utf-8") as f:
            prompt_text = f.read()

    if not prompt_text:
        print("Error: debes proporcionar --prompt o --prompt-file")
        sys.exit(1)

    client = SunoClient(session)
    print(f"🎵 Iniciando generación:")
    print(f"   - Título: {args.title}")
    print(f"   - Estilo (tags): {args.tags}")
    print(f"   - Modelo: {args.mv}")
    print(f"   - Instrumental: {args.instrumental}")

    clips = client.generate(
        prompt=prompt_text,
        tags=args.tags,
        title=args.title,
        instrumental=args.instrumental,
        mv=args.mv,
    )

    clip_ids = [c.get("id") for c in clips if c.get("id")]
    print(f"🚀 Generación lanzada. Clips creados: {clip_ids}")

    if args.no_wait:
        print("Opción --no-wait activa. Finalizado.")
        return

    completed_clips = client.wait_for_clips(clip_ids, timeout_sec=args.timeout)
    print("\n📦 Descargando pistas terminadas:")
    downloaded = []
    for c in completed_clips:
        if c.get("status") == "complete" and c.get("audio_url"):
            dest = client.download_clip(c)
            downloaded.append(dest)
        elif c.get("status") == "error":
            print(f"❌ El clip {c.get('id')} falló con error: {c.get('error_message')}")
        else:
            print(f"⚠️ El clip {c.get('id')} terminó con estado {c.get('status')}")

    print(f"\n🎉 ¡Proceso finalizado! {len(downloaded)} archivo(s) guardado(s).")


def cmd_feed(args, session: SunoSession):
    client = SunoClient(session)
    clips = client.get_feed()
    limit = args.limit
    print(f"--- Últimos {limit} clips en tu cuenta ---")
    for c in clips[:limit]:
        cid = c.get("id")
        title = c.get("title")
        st = c.get("status")
        created = c.get("created_at")
        audio = c.get("audio_url") or "Sin audio"
        print(f"• [{cid[:8]}] {title} | Estado: {st} | Creado: {created}\n  URL: {audio}")


def main():
    parser = argparse.ArgumentParser(description="Cliente directo para Suno AI")
    subparsers = parser.add_subparsers(dest="command", help="Comandos disponibles")

    # configure
    subparsers.add_parser("configure", help="Configura cookie/token de Suno")

    # check
    subparsers.add_parser("check", help="Comprueba conexión y muestra créditos")

    # feed
    p_feed = subparsers.add_parser("feed", help="Muestra los últimos clips generados")
    p_feed.add_argument("--limit", type=int, default=5, help="Número de clips a mostrar")

    # generate
    p_gen = subparsers.add_parser("generate", help="Genera una nueva pista")
    p_gen.add_argument("--title", required=True, help="Título de la pista")
    p_gen.add_argument("--tags", required=True, help="Estilo, género, instrumentos")
    p_gen.add_argument("--prompt", default="", help="Letra o descripción")
    p_gen.add_argument("--prompt-file", default="", help="Archivo de texto con la letra")
    p_gen.add_argument("--instrumental", action="store_true", help="Generar sin letra")
    p_gen.add_argument("--mv", default="chirp-v3-5", choices=["chirp-v3-5", "chirp-v4"], help="Versión del modelo")
    p_gen.add_argument("--timeout", type=int, default=300, help="Segundos máximos de espera")
    p_gen.add_argument("--no-wait", action="store_true", help="No esperar la descarga")

    args = parser.parse_args()
    session = SunoSession()

    if args.command == "configure":
        cmd_configure(args, session)
    elif args.command == "check":
        cmd_check(args, session)
    elif args.command == "feed":
        cmd_feed(args, session)
    elif args.command == "generate":
        cmd_generate(args, session)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
