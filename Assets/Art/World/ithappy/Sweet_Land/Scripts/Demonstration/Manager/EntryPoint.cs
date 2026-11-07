using UnityEngine;

namespace ithappy
{
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] private CharacterBase[] _characters;

        private void Start()
        {
            Application.targetFrameRate = 120;
            
            for (int i = 0; i < _characters.Length; i++)
            {
                // El Sendero: los personajes retirados de la escena (inactivos) no se arrancan; si no, su
                // NavMeshAgent apagado da errores de «not on NavMesh».
                if (_characters[i] == null || !_characters[i].gameObject.activeInHierarchy) continue;
                _characters[i].Initialize();
            }

            DistributePrioritiesEvenly();
        }

        private void DistributePrioritiesEvenly()
        {
            if (_characters.Length == 0) return;

            float priorityStep = 99f / (_characters.Length - 1);

            for (int i = 0; i < _characters.Length; i++)
            {
                if (_characters[i] == null) continue;
                _characters[i].SetPriority(Mathf.RoundToInt(i * priorityStep));
            }
        }
    }
}
