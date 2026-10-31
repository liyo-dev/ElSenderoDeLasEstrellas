using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ithappy
{
    public class Rnd_Animation : MonoBehaviour
    {
        Animator anim;
        float offsetAnim;

        [SerializeField] string titleAnim;

        void Start()
        {
            anim = GetComponent<Animator>();
            offsetAnim = Random.Range(0f, 1f);
            anim.Play(titleAnim, 0, offsetAnim);
        }

        void Update()
        {
        }
    }
}
