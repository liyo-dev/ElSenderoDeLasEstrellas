using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Recoge monedas pendientes y anota la ganancia neta desde el inicio de cada batalla.</summary>
public sealed class PasoBotinDeMonedas : IPasoDeCierre, IPasoConInicioDeBatalla
{
    private readonly Dictionary<ResultadoDeBatalla, FotoInicial> _fotos = new();
    public int Orden => 10;

    private sealed class FotoInicial
    {
        public Inventory inventario;
        public readonly List<SaldoInicial> saldos = new();
    }

    private readonly struct SaldoInicial
    {
        public readonly ItemData moneda;
        public readonly int cantidad;
        public SaldoInicial(ItemData moneda, int cantidad)
        {
            this.moneda = moneda;
            this.cantidad = cantidad;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoBotinDeMonedas());

    public void Iniciar(ResultadoDeBatalla resultado)
    {
        if (resultado == null || _fotos.ContainsKey(resultado)) return;
        var monedas = OrbDropService.Instance != null ? OrbDropService.Instance.Config?.CurrenciesForBattleReport : null;
        if (monedas == null || !PlayerService.TryGetComponent(out Inventory inventario, allowSceneLookup: false)) return;
        var foto = new FotoInicial { inventario = inventario };
        var ids = new HashSet<string>();
        for (int i = 0; i < monedas.Count; i++)
        {
            var item = monedas[i];
            if (item == null || item.usageKind != ItemData.ItemUsageKind.Currency ||
                string.IsNullOrEmpty(item.itemId) || !ids.Add(item.itemId)) continue;
            foto.saldos.Add(new SaldoInicial(item, inventario.Count(item.itemId)));
        }
        _fotos.Add(resultado, foto);
    }

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        // OnDied termina de avisar a todos los droppers antes de consultar los orbes creados.
        yield return null;
        BattleOrb.RecogerMonedasPendientes();
        if (!_fotos.TryGetValue(resultado, out var foto) || foto.inventario == null) yield break;
        foreach (var saldo in foto.saldos)
        {
            if (saldo.moneda == null) continue;
            int ganado = foto.inventario.Count(saldo.moneda.itemId) - saldo.cantidad;
            if (ganado > 0)
                resultado.AnotarBotin(new PremioDelBotin(saldo.moneda.GetLocalizedName(), saldo.moneda.icon, ganado));
        }
        _fotos.Remove(resultado);
    }

    public void Terminar(ResultadoDeBatalla resultado) => Cancelar(resultado);
    public void Cancelar(ResultadoDeBatalla resultado)
    {
        if (resultado != null) _fotos.Remove(resultado);
    }
}
