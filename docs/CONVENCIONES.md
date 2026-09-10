# Convenciones

## Escala y greybox
- Personaje: 1.8 m de alto.
- Altura de los ojos: 1.65 m. Es la que manda: el juego es en primera persona
  y todo lo que tenga que leerse se coloca respecto a esa línea, no al suelo.
- Puertas: 2.2 m.
- Techos: 3 m.
- Rejilla de 1 m, snapping siempre activado.
- Todo el nivel se construye con CSG.
- No se toca el arte hasta que el juego sea jugable de principio a fin. Hay una
  excepción hecha a sabiendas: el pase de aspecto de la sala de pruebas, en
  `ARTE.md`.
- **Ninguna textura es un archivo de imagen.** Todo sale de shaders en
  `assets/shaders/`. No se añaden PNG al proyecto hasta M7. Lo común —ruido,
  texeles cuadrados, tramado, facetado y temblor de vértices— vive en
  `retro.gdshaderinc` y lo incluyen todos: si el temblor de un material no sale
  de ahí, las piezas de un mismo bicho se despegan unas de otras.
- **Las piezas de una criatura llevan semilla.** Los shaders de hueso, chapa y
  tela declaran `instance uniform float piece_seed` y quien monta el bicho la
  reparte pieza a pieza en `_Ready` (ver `SkeletonRig.SeedPieces`). Sin ella,
  cuarenta huesos con el mismo material son cuarenta veces el mismo hueso y se
  lee como una textura repetida. Sale del NOMBRE del nodo, no de un contador: así
  añadir una pieza no recoloca las manchas de todas las demás.
- Piezas de CSG que se tocan: se entierra una en otra, nunca cara contra cara.
  Dos caras coplanares dejan artefactos.
- `Transform3D` en `.tscn` va por FILAS de la base. Si escribes una rotación a
  mano por columnas, sale transpuesta.
- **Nada de comentarios en los `.tscn`.** El editor los borra al reguardar. Lo
  que haya que explicar de una escena va en el script que la acompaña.
- **Lo que se puede desfasar, se genera al arrancar.** La malla de navegación
  (`LevelNavigation`), la siembra de hierba (`GrassField`) y los árboles secos
  (`DeadTree`) no se guardan en la escena: se construyen al cargar el nivel. No
  es por ahorrar trabajo, es porque un nivel de CSG se toca a diario y una malla
  guardada se queda vieja EN SILENCIO — se ve a un esqueleto atravesando una
  pared que ya no está donde dice su ruta, y eso se tarda media hora en atribuir
  a la navegación. Cuesta unas décimas al cargar y no puede estar desfasado.
- **Nada de eso se hace en `_Ready`.** El CSG del nivel construye su malla y su
  colisión en una llamada DIFERIDA al entrar en el árbol, y las órdenes al
  servidor de física encima no surten efecto hasta el siguiente paso. Lo que
  hornee geometría va en `CallDeferred`; lo que lance rayos, dos pasos de física
  después. Hacerlo antes no da error de ninguna clase: sale vacío.
- **Semilla fija en la escena para lo que se autora una vez.** El esqueleto
  reparte sus semillas por script porque se le añaden piezas; la mano del jugador
  las lleva escritas en el `.tscn`
  (`instance_shader_parameters/piece_seed`), porque son catorce piezas que no
  cambian. La regla que no cambia es que TIENE que haber semilla.

## Código
- C# es el lenguaje principal. GDScript solo para scripts triviales
  (puertas, triggers, carteles), que viven en `src/Props/`.
- `tools/` es GDScript de usar y tirar: escenas de desarrollo que no forman
  parte del juego y que no se cargan nunca desde `src/`. Se pueden borrar
  enteras sin romper nada. Ver `ARTE.md` §9.
- **Las animaciones se calculan, no se graban.** No hay `AnimationPlayer` en el
  proyecto y es a propósito: los tiempos de combate los escala cada arma con
  `SpeedScale`, así que una pista grabada se descuadra en cuanto se toca un
  número. Las poses salen de la fase y de lo recorrido de ella
  (`PhaseProgress`), y por eso no se pueden desincronizar del golpe.
- Métodos y clases en PascalCase.
- Señales en _snake_case.
- Nodos en PascalCase.
- Escena y script conviven en la misma carpeta de `src/`.

## Alcance
Definido en `DISENO.md`. Tres niveles, 3 tipos de enemigo, un jefe, 2-3 horas.
Lo que no esta en ese documento no esta en el juego.
Terminarlo tiene prioridad sobre ampliarlo.

## Entorno
- Godot 4.7.2 mono: `C:\Users\angel.panadero\Tools\Godot_4.7.2_mono\`
- .NET SDK 9 (lo exige el csproj que genera Godot 4.7).
- Binarios vía Git LFS; ver reglas en `.gitattributes`.
