# Medieval Nightmare — Diseño de la versión 1.0

Este documento describe **la versión 1.0 completa**. Todo lo que no está aquí,
no está en el juego. Si algo se te ocurre y no aparece en este documento, la
respuesta por defecto es no.

---

## 1. Qué es

Un juego de combate medieval en primera persona, de fantasía oscura, con
gráficos poligonales de principios de los 2000. Bajas a mazmorras a por
equipo, y cada sala que avanzas aumenta lo que puedes ganar y lo que puedes
perder. Si mueres, pierdes todo lo que llevabas encima.

**Primera persona.** Golpeas donde miras y solo sabes lo que tienes delante.
Se probó en tercera persona hasta M1 y la cámara resolvía sola los dos
problemas que tenía que resolver el jugador: ver de dónde viene el golpe y
saber qué tienes a la espalda.

Tres niveles, un jefe final, 2-3 horas.

## 2. Pilares

Tres. Si una decisión no sirve a uno de estos tres, no entra.

1. **El riesgo lo eliges tú.** Los golpes rápidos son seguros y hacen poco.
   El pesado te clava en el sitio casi dos segundos y mata de uno. Entre esas
   dos opciones está todo el combate.
2. **Salir vivo vale más que seguir.** La tensión no está en el combate, está
   en la decisión de avanzar una sala más o extraer con lo que ya tienes.
3. **La oscuridad es una mecánica.** La niebla y la falta de luz no decoran:
   son lo que te impide ver qué te va a matar. En primera persona solo ves lo
   que ilumina tu antorcha, y solo hacia donde miras: la cámara ya no te regala
   el flanco ni la espalda.

## 3. Referencias

- **Dark Messiah of Might & Magic** — el cuerpo a cuerpo en primera persona y
  el ritmo: ágil, pero con golpes cargados que te dejan vendido mientras salen.
  La referencia principal.
- **Blade of Darkness (2001)** — la época, la estética y el peso de los golpes.
  Su cámara no: esa parte se prueba en Dark Messiah.
- **Dark and Darker** — la decisión de extraer.
- **Hexen** — la paleta, y que un juego en primera persona pueda ir de pegar
  con un arma y no de disparar.

**En qué NO se parece:** no es un Souls (no hay estamina, ni parry, ni esquiva
con invulnerabilidad), no es un King's Field (se probó el combate lento en M1
y no funcionaba), no es un shooter (la ballesta es una herramienta con 6
virotes, no una forma de jugar), no tiene PvP, no es mundo abierto.

## 4. Bucle de juego

**Minuto a minuto.** Avanzas con la guardia alta por un pasillo oscuro. Oyes
algo. Decides si peleas, lo rodeas o retrocedes. Si peleas, aciertas la
distancia y el momento o recibes.

**Por incursión.** Entras a un nivel desde el campamento. Avanzas, matas,
recoges. En cada sala de extracción decides: salir con lo que llevas, o seguir.

**Progresión.** Extraer guarda tu equipo en el alijo y desbloquea el siguiente
nivel. No hay niveles de personaje ni experiencia: **tu progresión es tu
equipo**.

## 5. Combate

> Los números son valores de partida para ajustar jugando, no dogma.

**Sin barra de estamina.** Ningún ataque gasta recurso. Lo que te limita es
que ningún golpe se puede cancelar una vez empezado.

| Acción | Anticipación | Activo | Recuperación | Total | Movimiento |
|---|---|---|---|---|---|
| Ataque ligero | 0,12 s | 0,10 s | 0,22 s | 0,44 s | 55 % |
| Ataque pesado | 0,75 s | 0,20 s | 0,70 s | 1,65 s | 0 % |

- Los tiempos se multiplican por la velocidad del arma (0,8 a 1,35).
- **Los golpes barren un arco, no una línea.** Lo ancho que sea lo decide el arma
  (sección 6): ancho alcanza a varios de una pasada, estrecho obliga a apuntar.
  El filo va **de izquierda a derecha**, así que a quien tengas a la izquierda le
  llega antes y a quien tengas a la derecha le da tiempo a apartarse.
- **El ligero es la base.** Rápido, encadenable con 0,25 s de margen de
  entrada, y te deja moverte al 55 % de la velocidad de marcha.
- **El pesado es la apuesta.** Hace 2,5× de daño y te deja clavado en el sitio
  1,65 s. **Solo puedes apuntarlo durante la anticipación:** después la vista
  sigue girando libre, pero el arma se queda donde la dejaste. Ahí es donde te
  pegan.
- **Apuntas con la vista.** El arma sale exactamente por donde mira la cámara,
  sin giro progresivo: cualquier retraso entre lo que ves y por dónde sale el
  filo se siente roto. La única excepción es el pesado ya lanzado.
- **Sin fijado de objetivo.** En tercera persona hacía falta para saber a quién
  le pegabas; en primera lo hace la mira. Añadirlo ahora sería pelearse con la
  cámara del jugador.
- **La mira dice en qué fase estás.** No te ves a ti mismo, así que la cruz se
  colorea con los mismos colores que la telegrafía de los enemigos: ámbar en la
  anticipación, rojo mientras el filo está fuera, azul en la recuperación.
- **Vida del jugador:** 100. No se regenera.
- **Pociones:** curan 40. Máximo 3 por incursión, ocupan hueco de inventario.

### Defensa

Dos respuestas y ninguna vale para todo. Esa es toda la gracia.

| Defensa | Coste | Sirve contra | Movimiento |
|---|---|---|---|
| Bloqueo | Se rompe si aguantas, y solo cubre de frente | Todo menos los imparables | 45 % |
| Esquiva | 2 s de recarga | Todo, imparables incluidos | Desplazamiento fijo |

- **Bloqueo (mantener pulsado).** Reduce el daño recibido un **80 %**. Cubre un
  arco frontal de **120°**: por el flanco y por la espalda no bloqueas nada, y
  por eso pelear contra tres es colocarse, no aguantar. La guardia cubre hacia
  donde miras, así que girarse a tiempo es parte del bloqueo. Con la guardia
  alta te mueves al 45 %. Atacar o esquivar la bajan solas. **No hay parry.**
- **La guardia se rompe.** Aguanta **24 de daño bruto en 3 s**; al pasarse cede y
  tardas **10 s** en poder volver a levantarla. Veinticuatro son dos golpes de
  esqueleto seguidos, que es de donde sale la regla: el primero lo paras, el
  segundo te abre.
  - **Se cuenta el daño, no los impactos.** Así un golpe fuerte rompe la guardia
    él solo sin tener que ser un caso aparte, y los enemigos de M4 no obligan a
    volver aquí a añadir excepciones.
  - **La ventana se cuenta desde el último golpe parado.** Encajar dos golpes
    separados a lo largo de una pelea no rompe nada. Lo que la rompe es aguantar
    mientras te llueven, que es exactamente lo que se quería quitar.
  - **El golpe que la rompe sí lo paras.** La guardia cede después, no en lugar
    de. Al revés, el segundo impacto entraría a daño completo y eso no se lee: se
    sufre.
  - **Lo que no carga la guardia:** los imparables y todo lo que te llegue fuera
    del arco frontal. Ahí no hay guardia que romper, el golpe entra entero y ya
    estás pagando por no haberte girado.
  - Al probar M2 la guardia alta **no** ganaba sola, así que esto no arregla nada
    roto. Entra porque aguantar no puede ser una postura sostenible, y sin la
    rotura lo único que lo impedía era acordarse de girar.
- **Ataques imparables.** Algunos ataques atraviesan el bloqueo. Su anticipación
  se ve de otro color, así que se sabe antes de que salgan. Contra ellos no hay
  guardia: hay que esquivar. El esqueleto no tiene ninguno; el ogro y el jefe
  sí. Junto con la rotura, son lo que impide que la guardia alta sea la
  respuesta a todo: contra ellos no hay guardia que aguante, ni siquiera entera.
- **Esquiva.** Desplazamiento de **0,4 s a 9,5 m/s** en la dirección que marques,
  o de frente si no marcas ninguna. **No da invulnerabilidad:** te salva dejar de
  estar donde va a caer el golpe, no atravesarlo. **Recarga de 2 s**, contados
  desde que empieza. Es lo único que tiene enfriamiento **por diseño**: la
  guardia rota también te hace esperar, pero eso es un castigo, no un coste. Lo
  tiene justamente para que no puedas responder a todo con ella.
- Ni el bloqueo ni la esquiva se pueden usar a mitad de un golpe propio. La
  regla de que nada se cancela no tiene excepciones.

### Recibir un golpe

No te interrumpe, no te empuja y no te frena. Lo único que pasa es que **el
borde de la pantalla se tiñe de rojo**, con más fuerza cuanto más te ha quitado,
y **late solo** por debajo del 40 % de vida, más rápido cuanto peor estás. No hay
barra de vida ni números en pantalla: la vida se lee mirando el borde.

Tus golpes tampoco interrumpen al enemigo. **Probado en M2 y se queda así:**
castigar la anticipación ajena no sale gratis, porque el rato que pasas pegando
es rato que no estás defendiendo.

## 6. Jugador

**Inventario:** 12 huecos. Sin peso, sin gestión de espacio por tamaño.

**Equipo:** tres ranuras.

| Ranura | Qué admite |
|---|---|
| Principal | Un arma |
| Secundaria | Escudo o antorcha |
| Amuleto | Una habilidad |

Elegir antorcha significa renunciar al escudo. Esa decisión es el pilar 3
hecho mecánica.

**Armas (4).** Los tiempos de la tabla de combate son los mismos para todas.
Lo que cambia es **el arco que barre el golpe**, **el alcance** y **lo rápido
que sale**, que es de donde sale que cada una se juegue distinto y no solo
pegue distinto. **Ninguna te desplaza.** El arma decide dónde tienes que estar;
llegar hasta ahí es cosa tuya.

| Arma | Daño | Alcance | Velocidad | Arco ligero | Arco pesado | Matar un esqueleto |
|---|---|---|---|---|---|---|
| Espada corta | 12 | 1,8 m | 0,8 | 170° | 100° | 4 ligeros (1,4 s) o 2 pesados |
| Maza | 26 | 1,25 m | 1,0 | 45° | 60° | 2 ligeros (0,9 s) o 1 pesado |
| Mandoble | 30 | 2,6 m | 1,35 | 120° | **300°** | 2 ligeros (1,2 s) o 1 pesado |
| Ballesta | 20 | — | 1,0 | — | — | 2 virotes |

- **Espada corta: la que perdona.** La más rápida (0,8) y la que más ancho barre:
  **170°**, casi de lado a lado, así que alcanza a varios de una pasada y le da
  igual que el enemigo no esté justo delante. Pega poco por golpe, pero a 0,35 s
  el ciclo es la que menos te compromete: es con la que se aprende a pelear y con
  la que se sale de un apuro. En primera persona es además la que menos castiga
  fallar la puntería.
- **Maza: la que obliga a acercarse.** Pega más del doble que la espada, pero su
  arco es de 45° y su alcance de 1,25 m — **menos que los 1,7 m del esqueleto**.
  Para llegar con ella tienes que estar dentro del alcance del que te va a pegar,
  y hay que apuntarla: si el enemigo no está de frente, no le das.
- **Mandoble: el que responde a estar rodeado.** El más largo y el más lento. Su
  pesado no es un golpe, es **un giro de 300°** que alcanza a todo lo que tengas
  alrededor, incluso a tu espalda. A cambio te clava 2,2 s en el sitio. Sacarlo
  con tres encima es la mejor decisión del juego o la peor, y eso es el pilar 1.
  En primera persona vale doble: es la única respuesta a lo que no puedes ver.
- **Ballesta: la que se gasta.** Suelta un virote que **tarda en llegar** (18 m/s:
  a diez metros, medio segundo), así que a un enemigo que se mueve hay que
  adelantarle el tiro. Recarga 1 s. Entras con **6 virotes y no se reponen**: sin
  munición es peso muerto hasta que salgas. Es una herramienta para abrir una
  pelea concreta, no una forma de jugar.

Ninguna es mejor que otra: matan en tiempos parecidos y se diferencian por dónde
te obligan a estar.

**Amuletos (3).** Con enfriamiento, sin maná.

| Amuleto | Efecto | Enfriamiento |
|---|---|---|
| Ceniza | Cura 50 | 90 s |
| Faro | Luz fija que revela una sala | 60 s |
| Astilla | Proyectil, 35 de daño | 20 s |

## 7. Enemigos

Tres tipos y un jefe. Cada uno enseña una cosa distinta.

**Esqueleto** — el básico. Ataque único con 0,45 s de anticipación visible.
Enseña a leer la telegrafía. Vida 40, daño 12, velocidad 3 m/s.

**Bruja** — a distancia. Lanza desde lejos y retrocede si te acercas. Te obliga
a cruzar espacio abierto bajo presión, que es cuando aparecen los esqueletos.
Vida 30, daño 18.

**Ogro** — lento y devastador. Dos golpes te matan. Su ataque es **imparable**:
la guardia no vale de nada, hay que esquivarlo. Enseña a usar el espacio y a no
ser codicioso. Vida 180, daño 55.

**El Guardián del Pozo (jefe)** — un ogro coronado que invoca esqueletos.
Reutiliza dos enemigos existentes, que es exactamente por qué es el jefe.
Conserva el ataque imparable del ogro: no se le puede ganar de guardia. Vida 400.

## 8. Estructura

**El Campamento (hub).** Una sala. Un alijo, un portal, una hoguera. Sin NPC,
sin vendedores, sin monedas.

**Tres niveles**, hechos a mano, en este orden:

| Nivel | Tema | Enemigos | Duración |
|---|---|---|---|
| 1. La Cripta | Pasillos estrechos, poca luz | Esqueletos | ~30 min |
| 2. Las Minas | Alturas, huecos, emboscadas | Esqueletos, brujas | ~40 min |
| 3. El Pozo | Descenso, sala de jefe | Los tres + jefe | ~40 min |

Cada nivel tiene **dos salas de extracción**: una a mitad y otra antes del
tramo final.

**Regla de densidad.** Las salas de combate no bajan de 20 × 20 m y los
enemigos se colocan a **9 m o más entre sí**, que es su alcance de detección.
Así te los encuentras de uno en uno o de dos en dos, nunca en bloque. Un
enemigo rápido en un pasillo estrecho no es difícil, es injusto.

## 9. Muerte y extracción

- **Extraer:** guardas en el alijo todo lo que llevas. Vuelves al campamento.
  El nivel queda desbloqueado para siempre.
- **Morir:** pierdes el inventario **y lo equipado**. El alijo no se toca.
  Vuelves al campamento y el nivel se reinicia entero.
- **Suelo de seguridad:** siempre conservas una espada corta oxidada. Nunca
  puedes quedarte sin poder entrar.

Terminas el juego matando al Guardián del Pozo y extrayendo.

## 10. Fuera de la versión 1.0

Esta lista es la parte más importante del documento.

- Zonas exteriores, terreno, vegetación
- Niveles de personaje, experiencia, árboles de habilidades
- Monedas, vendedores, economía, crafteo, mejora de equipo
- Parry, estamina, esquiva con invulnerabilidad, combos
- Generación procedural
- Sigilo
- NPC, diálogo, misiones secundarias, doblaje
- Multijugador
- Dificultad seleccionable
- Idiomas más allá del español

## 11. Plan de trabajo

Un hito no está hecho hasta que se cumple su criterio. No se empieza el
siguiente hasta cerrar el anterior.

| # | Hito | Hecho cuando... |
|---|---|---|
| M0 | Controlador en primera persona | ✅ Camino, salto y caigo en una sala CSG |
| M1 | Combate mínimo | ✅ Mato a un esqueleto con las 3 armas y él puede matarme |
| M2 | Sala jugable | Una sala con 3 esqueletos, niebla y luz de antorcha, que se puede perder |
| M3 | Inventario, equipo y alijo | Entro con espada, cojo una maza, extraigo, y vuelvo a entrar con la maza |
| M4 | Los tres enemigos | Bruja y ogro completos, con su IA y su telegrafía |
| M5 | Nivel 1 completo | La Cripta jugable de entrada a extracción, en gris |
| M6 | **Juego completo en gris** | Los 3 niveles y el jefe, de principio a fin, sin arte |
| M7 | Arte y audio | Modelos, texturas, iluminación horneada, sonido |
| M8 | Pulido y build | Menús, guardado, ejecutable distribuible |

**M6 es el hito que importa.** Hasta M6 no se toca ni un solo asset de arte.
Si el juego no es divertido en cajas grises, no lo va a arreglar una textura.

> **Excepción hecha a sabiendas (10-09-2026).** Se hizo un pase de aspecto
> completo sobre la sala de pruebas —sillería, iluminación, atmósfera y rediseño
> del esqueleto— para ver a dónde puede llegar el juego. Está documentado en
> `ARTE.md`, junto con el presupuesto de rendimiento medido.
>
> **No cambia el plan.** El hito abierto sigue siendo M2 y el criterio sigue
> siendo el mismo: una sala con tres esqueletos que se puede perder. Que ahora
> se vea bien no cuenta como progreso. Si M2 no sale, lo que hay que tirar es
> esto, no el combate.

> **Segunda excepción, también a sabiendas (10-09-2026).** Se ha hecho un
> cementerio exterior nocturno (`src/Levels/Graveyard.tscn`) por la misma razón
> que la sala: ver a dónde puede llegar el juego fuera de una mazmorra, donde
> "todo lo ilumina una antorcha" deja de valer. Está en `ARTE.md` §6 bis.
>
> **Tampoco cambia el plan.** No es un nivel del juego, no aparece en la tabla de
> la sección 8 y no se juega para nada. El hito abierto sigue siendo M2.
>
> **Lo que M2 sí tiene ya:** los tres esqueletos rodean y se turnan para atacar en
> vez de amontonarse, andan por la sala con malla de navegación en vez de empujar
> los pilares, solo se despiertan si te VEN —de ahí que te los encuentres de uno
> en uno—, se entra por el pasillo y morir se lee como perder.
>
> **Jugado (11-09-2026).** Las cuatro preguntas tienen respuesta y están volcadas
> en §5, §12 y §13. En corto: morir por la espalda se siente el precio de la
> cámara y no se toca; la ballesta no se abusa y se queda en 6 virotes; el golpe
> recibido no interrumpe a nadie y se queda así. La guardia alta tampoco ganaba
> sola, pero **se añade la rotura de guardia** —24 de daño en 3 s, 10 s para
> volver— porque aguantar no puede ser una postura sostenible.
>
> **Lo que le falta a la rotura es que se lea.** De momento solo la cuenta el arma,
> que sale despedida y se queda caída mientras dura. El aviso claro es trabajo del
> HUD, y el HUD está diseñado pero no hecho: ver `HUD.md`.
>
> **El menú de pausa (Escape) es de M8 y se ha adelantado.** Cuesta una tarde y
> sin él no se puede probar nada sin matar el proceso.

## 12. Riesgos

| Riesgo | Cuándo se ve | Qué hacemos |
|---|---|---|
| ~~El combate lento resulta aburrido~~ | M1 | **Ocurrió.** Se cambió a ligero rápido + pesado comprometido |
| ~~Sin bloqueo ni esquiva, pelear contra 2+ enemigos es una carrera de daño~~ | M2 | **Resuelto.** Bloqueo de arco frontal y esquiva con 2 s de recarga |
| ~~La tercera persona le quita al jugador la decisión de mirar~~ | M2 | **Ocurrió.** Se cambió a primera persona. Con la cámara detrás, ver el flanco y la espalda salía gratis y el pilar 3 no se sostenía |
| ~~En primera persona no ves lo que tienes detrás y morir por la espalda parece injusto~~ | M2 | **Probado. No ocurre:** se siente el precio de la cámara, no un fallo. No se toca la IA ni la densidad |
| ~~El bloqueo vuelve el combate pasivo: aguantar con la guardia alta gana siempre~~ | M2 | **Probado. No ocurría**, pero la guardia pasa a romperse igual (§5): 24 de daño en 3 s la abren y tarda 10 s en volver |
| El ogro es el único modelo no humanoide y Mixamo no vale | M4 | Se sustituye por un humanoide grande y deforme |
| ~~La ballesta rompe el pilar 1: matar de lejos es quitarse el riesgo~~ | M2 | **Probado. No ocurre.** Los 6 virotes sin reposición bastan: se quedan en 6 |
| Las armas ya no comparten animación y M7 sale más caro | M7 | Asumido: el barrido ancho, el machaque y el giro son movimientos distintos. A favor: en primera persona solo hay que animar brazos y arma, no un cuerpo entero. Si M7 no da de sí, la ballesta cae antes que ninguna |
| El hub y el alijo se comen el tiempo | M3 | Se cae a: extraer = guardar partida, sin hub |

## 13. Decisiones abiertas

- ¿La antorcha se consume con el tiempo o es permanente?
- ~~¿Se ven las manos y el arma, o solo el arco del golpe?~~ **Decidido.** Se ve
  el arma y un puño agarrándola (`ARTE.md` §5 ter). El brazo entero sigue siendo
  trabajo de M7; lo que se adelantó fue la mano, que es lo que quita la sensación
  de arma flotando sin que haya que animar un cuerpo.
- ¿Cuántas pociones se pueden guardar en el alijo?
- **En M2 la antorcha sale gratis.** Ocupa la ranura secundaria, pero como el
  escudo todavía no existe como objeto, no renuncias a nada por llevarla. La
  decisión antorcha-o-escudo, que es el pilar 3 hecho mecánica, no se puede
  evaluar hasta M3.
- ~~¿Debería el golpe recibido interrumpir a alguien?~~ **Decidido jugando M2.**
  No interrumpe a nadie, ni a ti ni al enemigo, y se queda así.
