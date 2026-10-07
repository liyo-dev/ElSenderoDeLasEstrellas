16.- Prepárate para el viaje · encargos en paralelo · ForkNode · cap2-prepara-viaje-fork → cap2-prepara-viaje-victoria-0, cap2-prepara-viaje-boticaria-0
17.- Hablar con Victoria · WaitNpcInteractionNode · cap2-prepara-viaje-victoria-0 → cap2-prepara-viaje-victoria-1
18.- Victoria entrega la capa · PlayDialogueNode · cap2-prepara-viaje-victoria-1 → cap2-prepara-viaje-victoria-2
19.- Desbloquear capa de principiante · UnlockWardrobeItemNode · cap2-prepara-viaje-victoria-2 → cap2-prepara-viaje-victoria-3
20.- Tutorial · Equipo ▸ Capas · TutorialPromptNode · cap2-prepara-viaje-victoria-3 → cap2-prepara-viaje-victoria-4
21.- Completar encargo · capa · CompleteQuestStepsNode · cap2-prepara-viaje-victoria-4 → 
22.- Hablar con la boticaria · WaitNpcInteractionNode · cap2-prepara-viaje-boticaria-0 → cap2-prepara-viaje-boticaria-1
23.- La única poción de vida · PlayDialogueNode · cap2-prepara-viaje-boticaria-1 → cap2-prepara-viaje-boticaria-2
24.- Recibir poción de vida · GiveInventoryItemNode · cap2-prepara-viaje-boticaria-2 → cap2-prepara-viaje-boticaria-3
25.- Tutorial · usar consumibles · TutorialPromptNode · cap2-prepara-viaje-boticaria-3 → cap2-prepara-viaje-boticaria-4
26.- Completar encargo · poción · CompleteQuestStepsNode · cap2-prepara-viaje-boticaria-4 → 
