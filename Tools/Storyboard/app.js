(() => {
  "use strict";

  const SB = window.STORYBOARD;
  if (!SB || !Array.isArray(SB.paneles)) {
    document.body.innerHTML = "<p style='padding:24px;font-family:sans-serif'>Falta datos.js o no tiene paneles.</p>";
    return;
  }

  const CLAVE = "storyboard:" + SB.id;
  const GENERAL = "general";
  const ETIQUETA_GENERAL = "Plano / general";
  const PALETA = ["#f2c94c", "#6ec6f0", "#f28bd4", "#7bd88f", "#f2994a", "#bb86fc", "#e57373", "#9aa0a9"];
  const COLOR_GENERAL = "#8b929c";

  const estado = {
    anotaciones: [],
    modo: "nota",
    panelActivo: null,
    elementoPorPanel: {},
    siguienteId: 1,
    dibujo: null
  };

  const refs = new Map();

  const nodos = {
    titulo: document.getElementById("titulo"),
    subtitulo: document.getElementById("subtitulo"),
    paneles: document.getElementById("paneles"),
    leyendaModo: document.getElementById("leyenda-modo"),
    barra: document.getElementById("barra"),
    modal: document.getElementById("modal"),
    modalTexto: document.getElementById("modal-texto"),
    modalCopiar: document.getElementById("modal-copiar"),
    modalCerrar: document.getElementById("modal-cerrar"),
    btnResumen: document.getElementById("btn-resumen"),
    btnJson: document.getElementById("btn-json"),
    btnBorrar: document.getElementById("btn-borrar")
  };

  nodos.titulo.textContent = "Storyboard · " + SB.titulo;
  nodos.subtitulo.textContent = SB.subtitulo || "";
  document.title = "Storyboard · " + SB.titulo;

  const avisoNodo = document.createElement("div");
  avisoNodo.className = "aviso";
  document.body.appendChild(avisoNodo);
  let avisoTimer = null;

  function aviso(texto) {
    avisoNodo.textContent = texto;
    avisoNodo.classList.add("visible");
    clearTimeout(avisoTimer);
    avisoTimer = setTimeout(() => avisoNodo.classList.remove("visible"), 2200);
  }

  function panelDe(id) {
    return SB.paneles.find((p) => p.id === id);
  }

  function elementoPorDefecto(panel) {
    return panel.elementos && panel.elementos.length ? panel.elementos[0].id : GENERAL;
  }

  function elementoActual(panelId) {
    return estado.elementoPorPanel[panelId] || GENERAL;
  }

  function etiquetaDe(panelId, elementoId) {
    if (!elementoId || elementoId === GENERAL) return ETIQUETA_GENERAL;
    const panel = panelDe(panelId);
    const elemento = panel && panel.elementos ? panel.elementos.find((e) => e.id === elementoId) : null;
    return elemento ? elemento.etiqueta : elementoId;
  }

  function colorDe(panelId, elementoId) {
    if (!elementoId || elementoId === GENERAL) return COLOR_GENERAL;
    const panel = panelDe(panelId);
    if (!panel || !panel.elementos) return COLOR_GENERAL;
    const indice = panel.elementos.findIndex((e) => e.id === elementoId);
    return indice >= 0 ? PALETA[indice % PALETA.length] : COLOR_GENERAL;
  }

  function anotacionesDe(panelId) {
    return estado.anotaciones.filter((a) => a.panel === panelId);
  }

  // ── Montaje ──────────────────────────────────────────────────────────────────────────────

  function montarPaneles() {
    nodos.paneles.innerHTML = "";

    for (const panel of SB.paneles) {
      const articulo = document.createElement("article");
      articulo.className = "panel";
      articulo.dataset.panel = panel.id;

      const cabecera = document.createElement("header");
      cabecera.className = "panel-cabecera";
      cabecera.innerHTML =
        `<span class="panel-id">${escapar(panel.id)}</span>` +
        `<span class="panel-titulo">${escapar(panel.titulo || "")}</span>` +
        `<span class="panel-tiempo">${escapar(panel.tiempo || "")}</span>` +
        `<span class="panel-descripcion">${escapar(panel.descripcion || "")}</span>`;

      const media = document.createElement("div");
      media.className = "panel-media";

      const img = document.createElement("img");
      img.src = panel.imagen;
      img.alt = panel.titulo || panel.id;
      img.draggable = false;

      const canvas = document.createElement("canvas");
      canvas.width = 640;
      canvas.height = 360;
      canvas.className = "modo-" + estado.modo;

      media.append(img, canvas);

      const cuerpo = document.createElement("div");
      cuerpo.className = "panel-cuerpo";

      const leyenda = document.createElement("div");
      leyenda.className = "leyenda";

      const chips = new Map();
      crearChip(panel, GENERAL, ETIQUETA_GENERAL, leyenda, chips);
      for (const elemento of panel.elementos || []) {
        crearChip(panel, elemento.id, elemento.etiqueta, leyenda, chips);
      }

      const guion = document.createElement("p");
      guion.className = "guion";
      guion.textContent = panel.guion || "";

      const filaNota = document.createElement("div");
      filaNota.className = "nota-fila";

      const texto = document.createElement("textarea");
      texto.placeholder = "Escribe una nota para el elemento seleccionado…";
      texto.rows = 2;

      const botonNota = document.createElement("button");
      botonNota.className = "boton";
      botonNota.textContent = "Añadir nota";
      botonNota.type = "button";

      filaNota.append(texto, botonNota);

      const lista = document.createElement("div");
      lista.className = "lista-anotaciones";

      cuerpo.append(leyenda, guion, filaNota, lista);
      articulo.append(cabecera, media, cuerpo);
      nodos.paneles.appendChild(articulo);

      const ctx = canvas.getContext("2d");
      refs.set(panel.id, { articulo, img, canvas, ctx, chips, lista, texto });

      articulo.addEventListener("pointerdown", () => activarPanel(panel.id));
      texto.addEventListener("focus", () => activarPanel(panel.id));
      botonNota.addEventListener("click", () => agregarNota(panel.id));
      texto.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
          e.preventDefault();
          agregarNota(panel.id);
        }
      });

      canvas.addEventListener("pointerdown", (e) => empezarDibujo(e, panel.id));
      canvas.addEventListener("contextmenu", (e) => e.preventDefault());

      img.addEventListener("load", () => {
        const ancho = img.naturalWidth || 640;
        const alto = img.naturalHeight || 360;
        if (canvas.width !== ancho || canvas.height !== alto) {
          canvas.width = ancho;
          canvas.height = alto;
        }
        redibujar(panel.id);
      });

      if (img.complete) {
        const ancho = img.naturalWidth || 640;
        const alto = img.naturalHeight || 360;
        canvas.width = ancho;
        canvas.height = alto;
        redibujar(panel.id);
      }
    }

    if (SB.paneles.length) activarPanel(SB.paneles[0].id);
    for (const panel of SB.paneles) renderLista(panel.id);
    actualizarChips();
    actualizarPista();
  }

  function crearChip(panel, elementoId, etiqueta, contenedor, mapa) {
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "chip";
    chip.dataset.elemento = elementoId;
    chip.style.setProperty("--color", colorDe(panel.id, elementoId));
    chip.innerHTML = `<span class="punto"></span><span>${escapar(etiqueta)}</span><span class="cuenta"></span>`;

    chip.addEventListener("click", (e) => {
      e.stopPropagation();
      estado.elementoPorPanel[panel.id] = elementoId;
      activarPanel(panel.id);
    });

    contenedor.appendChild(chip);
    mapa.set(elementoId, chip);
  }

  // ── Estado de la interfaz ───────────────────────────────────────────────────────────────

  function activarPanel(panelId) {
    estado.panelActivo = panelId;
    if (!estado.elementoPorPanel[panelId]) {
      estado.elementoPorPanel[panelId] = elementoPorDefecto(panelDe(panelId));
    }

    for (const [id, ref] of refs) {
      ref.articulo.classList.toggle("activo", id === panelId);
    }
    actualizarChips();
    actualizarPista();
  }

  function actualizarChips() {
    for (const [panelId, ref] of refs) {
      const activo = estado.elementoPorPanel[panelId] || GENERAL;
      const cuenta = new Map();
      for (const a of anotacionesDe(panelId)) {
        const clave = a.elemento || GENERAL;
        cuenta.set(clave, (cuenta.get(clave) || 0) + 1);
      }

      for (const [elementoId, chip] of ref.chips) {
        chip.classList.toggle("activo", estado.panelActivo === panelId && elementoId === activo);
        chip.style.setProperty("--color", colorDe(panelId, elementoId));
        const n = cuenta.get(elementoId) || 0;
        chip.querySelector(".cuenta").textContent = n > 0 ? String(n) : "";
      }
    }
  }

  function actualizarPista() {
    if (!estado.panelActivo) {
      nodos.leyendaModo.textContent = "Selecciona un plano y un elemento.";
      return;
    }
    const elemento = etiquetaDe(estado.panelActivo, elementoActual(estado.panelActivo));
    nodos.leyendaModo.textContent = `${estado.panelActivo} · ${elemento} · ${nombreModo(estado.modo)}`;
  }

  function nombreModo(modo) {
    return { nota: "Nota", flecha: "Flecha", trazo: "Trazo libre", caja: "Caja" }[modo] || modo;
  }

  function ponerModo(modo) {
    estado.modo = modo;
    for (const boton of nodos.barra.querySelectorAll("[data-modo]")) {
      boton.classList.toggle("activa", boton.dataset.modo === modo);
    }
    for (const ref of refs.values()) {
      ref.canvas.classList.toggle("modo-nota", modo === "nota");
    }
    actualizarPista();
  }

  nodos.barra.addEventListener("click", (e) => {
    const boton = e.target.closest("[data-modo]");
    if (!boton) return;
    ponerModo(boton.dataset.modo);
  });

  // ── Dibujo ──────────────────────────────────────────────────────────────────────────────

  function puntoDe(e, canvas) {
    const r = canvas.getBoundingClientRect();
    const x = r.width > 0 ? (e.clientX - r.left) / r.width : 0;
    const y = r.height > 0 ? (e.clientY - r.top) / r.height : 0;
    return { x: limitar(x), y: limitar(y) };
  }

  function limitar(v) {
    return Math.max(0, Math.min(1, v));
  }

  function empezarDibujo(e, panelId) {
    if (estado.modo === "nota") {
      activarPanel(panelId);
      return;
    }
    if (e.button !== undefined && e.button !== 0) return;

    const ref = refs.get(panelId);
    if (!ref) return;

    e.preventDefault();
    activarPanel(panelId);

    const p = puntoDe(e, ref.canvas);
    estado.dibujo = {
      panelId,
      tipo: estado.modo,
      pointerId: e.pointerId,
      puntos: [p],
      p1: p,
      p2: p,
      caja: { x: p.x, y: p.y, w: 0, h: 0 }
    };

    try { ref.canvas.setPointerCapture(e.pointerId); } catch { /* algunos navegadores no lo permiten */ }
    redibujar(panelId);
  }

  function moverDibujo(e) {
    const d = estado.dibujo;
    if (!d || e.pointerId !== d.pointerId) return;

    const ref = refs.get(d.panelId);
    if (!ref) return;

    const p = puntoDe(e, ref.canvas);
    if (d.tipo === "trazo") {
      const ultimo = d.puntos[d.puntos.length - 1];
      if (Math.hypot(p.x - ultimo.x, p.y - ultimo.y) > 0.002) d.puntos.push(p);
    } else if (d.tipo === "flecha") {
      d.p2 = p;
    } else if (d.tipo === "caja") {
      d.caja = {
        x: Math.min(d.p1.x, p.x),
        y: Math.min(d.p1.y, p.y),
        w: Math.abs(p.x - d.p1.x),
        h: Math.abs(p.y - d.p1.y)
      };
    }

    redibujar(d.panelId);
  }

  function terminarDibujo(e) {
    const d = estado.dibujo;
    if (!d || e.pointerId !== d.pointerId) return;

    const ref = refs.get(d.panelId);
    if (!ref) return;

    try { ref.canvas.releasePointerCapture(e.pointerId); } catch { /* nada */ }

    const p = puntoDe(e, ref.canvas);
    if (d.tipo === "flecha") d.p2 = p;
    if (d.tipo === "caja") {
      d.caja = {
        x: Math.min(d.p1.x, p.x),
        y: Math.min(d.p1.y, p.y),
        w: Math.abs(p.x - d.p1.x),
        h: Math.abs(p.y - d.p1.y)
      };
    }

    const vale =
      (d.tipo === "trazo" && d.puntos.length >= 2) ||
      (d.tipo === "flecha" && Math.hypot(d.p2.x - d.p1.x, d.p2.y - d.p1.y) > 0.015) ||
      (d.tipo === "caja" && d.caja.w > 0.01 && d.caja.h > 0.01);

    estado.dibujo = null;

    if (!vale) {
      redibujar(d.panelId);
      return;
    }

    const elemento = elementoActual(d.panelId);
    const anotacion = {
      id: estado.siguienteId++,
      panel: d.panelId,
      elemento,
      tipo: d.tipo,
      color: colorDe(d.panelId, elemento),
      texto: ""
    };

    if (d.tipo === "trazo") anotacion.puntos = d.puntos.map((q) => [redondear(q.x), redondear(q.y)]);
    if (d.tipo === "flecha") anotacion.p1 = [redondear(d.p1.x), redondear(d.p1.y)], anotacion.p2 = [redondear(d.p2.x), redondear(d.p2.y)];
    if (d.tipo === "caja") anotacion.caja = {
      x: redondear(d.caja.x), y: redondear(d.caja.y),
      w: redondear(d.caja.w), h: redondear(d.caja.h)
    };

    estado.anotaciones.push(anotacion);
    guardar();
    renderLista(d.panelId);
    actualizarChips();
    redibujar(d.panelId);
    aviso("Anotación añadida a " + etiquetaDe(d.panelId, elemento) + ".");
  }

  function cancelarDibujo() {
    if (!estado.dibujo) return;
    const panelId = estado.dibujo.panelId;
    estado.dibujo = null;
    redibujar(panelId);
  }

  function redondear(v) {
    return Math.round(v * 1000) / 1000;
  }

  function redibujar(panelId) {
    const ref = refs.get(panelId);
    if (!ref) return;

    const { ctx, canvas } = ref;
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    for (const a of anotacionesDe(panelId)) dibujarAnotacion(ctx, canvas, a, 1);

    if (estado.dibujo && estado.dibujo.panelId === panelId) {
      const d = estado.dibujo;
      const provisional = {
        tipo: d.tipo,
        color: colorDe(panelId, elementoActual(panelId)),
        puntos: d.tipo === "trazo" ? d.puntos.map((q) => [q.x, q.y]) : null,
        p1: d.p1 ? [d.p1.x, d.p1.y] : null,
        p2: d.p2 ? [d.p2.x, d.p2.y] : null,
        caja: d.caja
      };
      dibujarAnotacion(ctx, canvas, provisional, 0.65);
    }
  }

  function dibujarAnotacion(ctx, canvas, a, alpha) {
    const ancho = Math.max(2, canvas.width / 320);
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = a.color || COLOR_GENERAL;
    ctx.fillStyle = a.color || COLOR_GENERAL;
    ctx.lineWidth = ancho;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";

    if (a.tipo === "trazo" && a.puntos && a.puntos.length) {
      const puntos = a.puntos;
      if (puntos.length === 1) {
        ctx.beginPath();
        ctx.arc(puntos[0][0] * canvas.width, puntos[0][1] * canvas.height, ancho * 1.6, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.beginPath();
        ctx.moveTo(puntos[0][0] * canvas.width, puntos[0][1] * canvas.height);
        for (let i = 1; i < puntos.length; i++) {
          ctx.lineTo(puntos[i][0] * canvas.width, puntos[i][1] * canvas.height);
        }
        ctx.stroke();
      }
    } else if (a.tipo === "flecha" && a.p1 && a.p2) {
      const x1 = a.p1[0] * canvas.width, y1 = a.p1[1] * canvas.height;
      const x2 = a.p2[0] * canvas.width, y2 = a.p2[1] * canvas.height;
      if (Math.hypot(x2 - x1, y2 - y1) < ancho * 1.6) {
        ctx.beginPath();
        ctx.arc(x1, y1, ancho * 1.6, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();

        const ang = Math.atan2(y2 - y1, x2 - x1);
        const largo = Math.max(10, canvas.width / 50);
        ctx.beginPath();
        ctx.moveTo(x2, y2);
        ctx.lineTo(x2 - largo * Math.cos(ang - 0.45), y2 - largo * Math.sin(ang - 0.45));
        ctx.lineTo(x2 - largo * Math.cos(ang + 0.45), y2 - largo * Math.sin(ang + 0.45));
        ctx.closePath();
        ctx.fill();
      }
    } else if (a.tipo === "caja" && a.caja) {
      if (a.caja.w * canvas.width < 2 && a.caja.h * canvas.height < 2) {
        ctx.beginPath();
        ctx.arc(a.caja.x * canvas.width, a.caja.y * canvas.height, ancho * 1.6, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.strokeRect(
          a.caja.x * canvas.width,
          a.caja.y * canvas.height,
          a.caja.w * canvas.width,
          a.caja.h * canvas.height
        );
      }
    }

    ctx.restore();
  }

  // ── Notas ───────────────────────────────────────────────────────────────────────────────

  function agregarNota(panelId) {
    const ref = refs.get(panelId);
    if (!ref) return;

    const texto = ref.texto.value.trim();
    if (!texto) {
      aviso("Escribe algo antes de añadir la nota.");
      ref.texto.focus();
      return;
    }

    const elemento = elementoActual(panelId);
    estado.anotaciones.push({
      id: estado.siguienteId++,
      panel: panelId,
      elemento,
      tipo: "nota",
      color: colorDe(panelId, elemento),
      texto
    });

    ref.texto.value = "";
    guardar();
    renderLista(panelId);
    actualizarChips();
    aviso("Nota añadida a " + etiquetaDe(panelId, elemento) + ".");
  }

  function renderLista(panelId) {
    const ref = refs.get(panelId);
    if (!ref) return;

    ref.lista.innerHTML = "";
    const anotaciones = anotacionesDe(panelId);
    if (anotaciones.length === 0) {
      ref.lista.innerHTML = `<p class="vacio">Sin anotaciones todavía. Elige un elemento y escribe o dibuja.</p>`;
      return;
    }

    for (const a of anotaciones) {
      const fila = document.createElement("div");
      fila.className = "anotacion";
      fila.style.setProperty("--color", a.color || COLOR_GENERAL);

      const punto = document.createElement("span");
      punto.className = "punto";

      const texto = document.createElement("div");
      const quien = document.createElement("span");
      quien.className = "quien";
      quien.textContent = etiquetaDe(panelId, a.elemento);

      const tipo = document.createElement("span");
      tipo.className = "tipo";
      tipo.textContent = " · " + a.tipo + " · ";

      const detalle = document.createElement("span");
      detalle.className = "texto";
      detalle.textContent = descripcionDe(a);

      texto.append(quien, tipo, detalle);

      const borrar = document.createElement("button");
      borrar.type = "button";
      borrar.title = "Borrar esta anotación";
      borrar.textContent = "×";
      borrar.addEventListener("click", (e) => {
        e.stopPropagation();
        estado.anotaciones = estado.anotaciones.filter((x) => x.id !== a.id);
        guardar();
        renderLista(panelId);
        actualizarChips();
        redibujar(panelId);
      });

      fila.append(punto, texto, borrar);
      fila.addEventListener("click", () => {
        estado.elementoPorPanel[panelId] = a.elemento || GENERAL;
        activarPanel(panelId);
      });

      ref.lista.appendChild(fila);
    }
  }

  function descripcionDe(a) {
    if (a.tipo === "nota") return a.texto || "(sin texto)";
    if (a.tipo === "flecha" && a.p1 && a.p2) {
      return `flecha de (${a.p1[0]}, ${a.p1[1]}) a (${a.p2[0]}, ${a.p2[1]})`;
    }
    if (a.tipo === "trazo" && a.puntos) {
      return `trazo de ${a.puntos.length} puntos`;
    }
    if (a.tipo === "caja" && a.caja) {
      return `caja en (${a.caja.x}, ${a.caja.y}) de ${a.caja.w} × ${a.caja.h}`;
    }
    return a.tipo;
  }

  // ── Guardado y exportación ──────────────────────────────────────────────────────────────

  function guardar() {
    try {
      localStorage.setItem(CLAVE, JSON.stringify(estado.anotaciones));
    } catch {
      /* file:// con almacenamiento bloqueado: se queda en memoria hasta exportar */
    }
  }

  function cargar() {
    try {
      const crudo = localStorage.getItem(CLAVE);
      if (!crudo) return;
      const datos = JSON.parse(crudo);
      if (!Array.isArray(datos)) return;
      estado.anotaciones = datos.filter((a) => a && a.panel && a.tipo);
      estado.siguienteId = 1 + estado.anotaciones.reduce((max, a) => Math.max(max, Number(a.id) || 0), 0);
    } catch {
      estado.anotaciones = [];
    }
  }

  function construirJSON() {
    return {
      version: 1,
      storyboard: SB.id,
      titulo: SB.titulo,
      fecha: new Date().toISOString(),
      anotaciones: estado.anotaciones
    };
  }

  function descargarJSON() {
    const blob = new Blob([JSON.stringify(construirJSON(), null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement("a");
    enlace.href = url;
    enlace.download = `anotaciones-${SB.id}.json`;
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    aviso("JSON descargado.");
  }

  function construirResumen() {
    const total = estado.anotaciones.length;
    const lineas = [];
    lineas.push(`# Storyboard: ${SB.titulo}`);
    lineas.push("");
    lineas.push(`Generado el ${new Date().toLocaleString("es-ES")} · ${total} anotación(es).`);
    lineas.push("");

    if (total === 0) {
      lineas.push("(Sin anotaciones.)");
      return lineas.join("\n");
    }

    for (const panel of SB.paneles) {
      const anotaciones = anotacionesDe(panel.id);
      if (anotaciones.length === 0) continue;

      lineas.push(`## ${panel.id} — ${panel.titulo}${panel.tiempo ? ` (${panel.tiempo})` : ""}`);
      if (panel.descripcion) lineas.push(panel.descripcion);
      if (panel.guion) lineas.push(`> ${panel.guion}`);
      lineas.push("");

      for (const a of anotaciones) {
        const quien = etiquetaDe(panel.id, a.elemento);
        if (a.tipo === "nota") {
          lineas.push(`- **${quien}** · nota: ${String(a.texto || "").replace(/\s+/g, " ").trim()}`);
        } else {
          lineas.push(`- **${quien}** · ${descripcionDe(a)}`);
        }
      }
      lineas.push("");
    }

    return lineas.join("\n").trim() + "\n";
  }

  async function copiarResumen() {
    const texto = construirResumen();
    try {
      await navigator.clipboard.writeText(texto);
      aviso("Resumen copiado. Pégalo en el chat.");
    } catch {
      mostrarModal(texto);
    }
  }

  function mostrarModal(texto) {
    nodos.modalTexto.value = texto;
    nodos.modal.classList.remove("oculto");
    setTimeout(() => {
      nodos.modalTexto.focus();
      nodos.modalTexto.select();
    }, 0);
  }

  function cerrarModal() {
    nodos.modal.classList.add("oculto");
  }

  nodos.modalCopiar.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(nodos.modalTexto.value);
      aviso("Copiado.");
    } catch {
      nodos.modalTexto.focus();
      nodos.modalTexto.select();
      document.execCommand("copy");
      aviso("Copiado (método clásico).");
    }
  });

  nodos.modalCerrar.addEventListener("click", cerrarModal);
  nodos.modal.addEventListener("click", (e) => {
    if (e.target === nodos.modal) cerrarModal();
  });

  nodos.btnResumen.addEventListener("click", copiarResumen);
  nodos.btnJson.addEventListener("click", descargarJSON);
  nodos.btnBorrar.addEventListener("click", () => {
    if (!confirm("¿Borrar todas las anotaciones de este storyboard?")) return;
    estado.anotaciones = [];
    estado.siguienteId = 1;
    guardar();
    for (const panel of SB.paneles) renderLista(panel.id);
    actualizarChips();
    for (const panel of SB.paneles) redibujar(panel.id);
    aviso("Anotaciones borradas.");
  });

  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      if (!nodos.modal.classList.contains("oculto")) cerrarModal();
      else cancelarDibujo();
    }
  });

  // Los movimientos se escuchan en la ventana, no solo en el lienzo: así el trazo sigue vivo
  // aunque el puntero salga del panel o el navegador esté capturando el gesto.
  window.addEventListener("pointermove", moverDibujo);
  window.addEventListener("pointerup", terminarDibujo);
  window.addEventListener("pointercancel", cancelarDibujo);

  function escapar(texto) {
    return String(texto == null ? "" : texto)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;");
  }

  // ── Arranque ────────────────────────────────────────────────────────────────────────────

  cargar();
  montarPaneles();
  for (const panel of SB.paneles) redibujar(panel.id);
  ponerModo("nota");
})();
