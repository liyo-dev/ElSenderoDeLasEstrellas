using UnityEngine;
using UnityEngine.UI;

/// Destello en pantalla cuando el personaje que lleva el jugador recibe un golpe. Sigue al cuerpo
/// registrado en PlayerService: al cambiar de personaje escucha al nuevo.
public class PlayerDamageScreenEffects : MonoBehaviour
{
    [SerializeField] private Image damageImage;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    private PlayerHealthSystem _playerHealthSystem;
    private Coroutine _destello;

    private void OnEnable()
    {
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        Escuchar(PlayerService.TryGetComponent(out PlayerHealthSystem salud) ? salud : null);
    }

    private void OnDisable()
    {
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        Escuchar(null);
    }

    private void AlRegistrarJugador(GameObject _)
    {
        PlayerService.TryGetComponent(out PlayerHealthSystem salud, allowSceneLookup: false);
        Escuchar(salud);
    }

    private void Escuchar(PlayerHealthSystem salud)
    {
        if (salud == _playerHealthSystem) return;
        if (_playerHealthSystem != null) _playerHealthSystem.OnDamageTaken.RemoveListener(AlRecibirGolpe);
        _playerHealthSystem = salud;
        if (_playerHealthSystem != null) _playerHealthSystem.OnDamageTaken.AddListener(AlRecibirGolpe);
    }

    private void AlRecibirGolpe(float damage, float health)
    {
        if (damageImage == null) return;
        if (_destello != null) StopCoroutine(_destello);
        _destello = StartCoroutine(FlashDamageImage());
    }

    private System.Collections.IEnumerator FlashDamageImage()
    {
        damageImage.color = damageColor;
        yield return new WaitForSeconds(flashDuration);
        damageImage.color = Color.clear;
        _destello = null;
    }
}
