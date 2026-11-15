// Storyboard de prueba: los seis planos del previz real de Cap. 3 · El espejo.
// Cada panel lleva su imagen, su leyenda de elementos y la línea del guion que le toca.
// Para otro storyboard, se copia este archivo y se cambian paneles, imágenes y elementos.
window.STORYBOARD = {
  id: "cap3-01-el-espejo",
  titulo: "Cap. 3 · El espejo",
  subtitulo: "Prueba del storyboard anotado con el previz real de TorreDeLiam. Elige un plano, selecciona un elemento de su leyenda y escribe o dibuja: la anotación queda pegada a ese elemento.",
  paneles: [
    {
      id: "P01",
      titulo: "General de la torre",
      tiempo: "0,0 – 2,0 s",
      descripcion: "Liam sentado a la mesa; la bola de visión delante.",
      guion: "plano general liam mira=foco_general altura_foco=0 desde=cam_general altura=1.5 lente=65",
      imagen: "../../Guiones/previz/Cap3_01_ElEspejo/001.png",
      elementos: [
        { id: "liam", etiqueta: "Liam" },
        { id: "tobi", etiqueta: "Tobías" },
        { id: "bola", etiqueta: "Bola de visión" },
        { id: "ventana", etiqueta: "Ventana" },
        { id: "camara", etiqueta: "Cámara" }
      ]
    },
    {
      id: "P02",
      titulo: "Medio desde la bola",
      tiempo: "2,0 – 8,3 s",
      descripcion: "La bola en primer término y Liam detrás, mirándola.",
      guion: "plano medio vision liam desde=cam_bola altura=0.95 lente=50",
      imagen: "../../Guiones/previz/Cap3_01_ElEspejo/002.png",
      elementos: [
        { id: "liam", etiqueta: "Liam" },
        { id: "bola", etiqueta: "Bola de visión" },
        { id: "tobi", etiqueta: "Tobías (en la visión)" },
        { id: "camara", etiqueta: "Cámara" }
      ]
    },
    {
      id: "P03",
      titulo: "Primer plano de Liam",
      tiempo: "8,3 – 9,5 s",
      descripcion: "Liam, de cerca, hablando con su hermano.",
      guion: "plano primer liam desde=cam_cara altura=1.25 lente=40",
      imagen: "../../Guiones/previz/Cap3_01_ElEspejo/003.png",
      elementos: [
        { id: "liam", etiqueta: "Liam" },
        { id: "bola", etiqueta: "Bola de visión" },
        { id: "camara", etiqueta: "Cámara" }
      ]
    },
    {
      id: "P04",
      titulo: "Medio de Liam",
      tiempo: "9,5 – 12,4 s",
      descripcion: "Liam decide contar lo del aro.",
      guion: "plano medio liam desde=cam_sienta altura=1.25 lente=40",
      imagen: "../../Guiones/previz/Cap3_01_ElEspejo/004.png",
      elementos: [
        { id: "liam", etiqueta: "Liam" },
        { id: "bola", etiqueta: "Bola de visión" },
        { id: "camara", etiqueta: "Cámara" }
      ]
    },
    {
      id: "P05",
      titulo: "Medio desde la bola (vuelta)",
      tiempo: "12,4 – 18,4 s",
      descripcion: "La bola delante; Tobías responde desde la visión.",
      guion: "plano medio vision liam desde=cam_bola altura=0.95 lente=50",
      imagen: "../../Guiones/previz/Cap3_01_ElEspejo/005.png",
      elementos: [
        { id: "liam", etiqueta: "Liam" },
        { id: "bola", etiqueta: "Bola de visión" },
        { id: "tobi", etiqueta: "Tobías (en la visión)" },
        { id: "camara", etiqueta: "Cámara" }
      ]
    },
    {
      id: "P06",
      titulo: "Primer plano de Liam (final)",
      tiempo: "18,4 – 28,8 s",
      descripcion: "Liam se queda con lo del aro; la escena acaba en negro.",
      guion: "plano primer liam desde=cam_cara altura=1.25 lente=40",
      imagen: "../../Guiones/previz/Cap3_01_ElEspejo/006.png",
      elementos: [
        { id: "liam", etiqueta: "Liam" },
        { id: "bola", etiqueta: "Bola de visión" },
        { id: "camara", etiqueta: "Cámara" }
      ]
    }
  ]
};
