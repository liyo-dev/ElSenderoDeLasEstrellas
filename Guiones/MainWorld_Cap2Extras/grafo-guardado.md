2.3d · Aprende Bola Prisma (tras los cubos de Eldran) · UnlockAbilitiesNode · cap2-desbloqueo-bola-prisma · entradas 0 → 
2.9 · Primera receta: Cúpula Estelar · UnlockAbilitiesNode · cap2-desbloqueo-cupula-estelar · entradas 0 → cap2-tutorial-cupula-estelar
2.9 · Tutorial · combo mágico (Y → X → B → A) · TutorialPromptNode · cap2-tutorial-cupula-estelar · entradas 1 → 
2.10 · Will sostiene la figura: aprende Levitación · UnlockAbilitiesNode · cap2-desbloqueo-levitacion · entradas 0 → 
2.7 · Contrabandistas: ¿cómo actúa Will? · ForkNode · cap2-contrabandistas-0 · entradas 0 → cap2-contrabandistas-ataque-0, cap2-contrabandistas-pluma-0, cap2-contrabandistas-jaula-0
2.7 · Will entra en el claro · WaitCustomEventNode · cap2-contrabandistas-ataque-0 · entradas 1 → cap2-contrabandistas-ataque-1
2.7 · ¿Ya había elegido el sigilo? · BranchFlagNode · cap2-contrabandistas-ataque-1 · entradas 1 → (fin), cap2-contrabandistas-ataque-2
2.7 · Elige pelear · SetFlagNode · cap2-contrabandistas-ataque-2 · entradas 1 → cap2-contrabandistas-ataque-3
2.7 · Contrabandistas vencidos · WaitCustomEventNode · cap2-contrabandistas-ataque-3 · entradas 1 → cap2-contrabandistas-fin
2.7 · Coge la pluma brillante · WaitForItemAddedNode · cap2-contrabandistas-pluma-0 · entradas 1 → cap2-contrabandistas-pluma-1
2.7 · ¿Ya había empezado a pelear? · BranchFlagNode · cap2-contrabandistas-pluma-1 · entradas 1 → (fin), cap2-contrabandistas-pluma-2
2.7 · Elige avisar a la guardia · SetFlagNode · cap2-contrabandistas-pluma-2 · entradas 1 → cap2-contrabandistas-fin
2.7 · Arde una jaula · WaitCustomEventNode · cap2-contrabandistas-jaula-0 · entradas 1 → cap2-contrabandistas-jaula-1
2.7 · Consecuencia: jaulas quemadas · SetFlagNode · cap2-contrabandistas-jaula-1 · entradas 1 → 
2.7 · Cap. 2 · Tras los contrabandistas · CheckpointNode · cap2-contrabandistas-fin · entradas 2 → 
