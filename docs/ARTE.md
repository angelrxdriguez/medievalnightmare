# Dirección de arte — prueba de aspecto

> **Esto se salta la norma de `DISENO.md` §11:** "hasta M6 no se toca ni un solo
> asset de arte". Se hizo a propósito y por encargo, para ver a dónde puede
> llegar el juego antes de comprometerse. **No es permiso para seguir haciendo
> arte.** El siguiente hito sigue siendo M2, en gris o con esto puesto, da igual:
> lo que decide si el juego sigue es si se puede perder una sala con tres
> esqueletos, no cómo se ve.
>
> Lo que sí sirve de aquí: la paleta, los números de iluminación y el
> presupuesto de rendimiento medido. Eso se conserva aunque el arte final se
> rehaga entero en M7.

---

## 1. La regla

**Nada de imágenes.** No hay un solo PNG en el proyecto y no lo va a haber
hasta M7. Toda la textura sale de seis shaders procedurales —piedra, hueso,
hierro, llama, acero y mango— sobre una caja de herramientas común
(`assets/shaders/retro.gdshaderinc`). Es más barato de
mantener, no tiene tiling visible, se ajusta cambiando un número, y sobre todo:
no crea una biblioteca de assets que haya que rehacer cuando cambie el estilo.

## 2. Qué hace que parezca 2001 y no ahora

Tres decisiones, y ninguna es el número de polígonos.

**Texeles cuadrados.** Antes de evaluar nada, las coordenadas se redondean a una
rejilla (`texels_per_meter`). Eso convierte el ruido continuo en píxeles
cuadrados del tamaño que tendría una textura de 128 o 256 px, con sus escalones.
Es la diferencia entre "procedural moderno" y "esto lo pintó alguien a mano".

| Superficie | Texeles/m | Equivale a |
|---|---|---|
| Muro | 56 | ~50 px por sillar de 0,9 m |
| Suelo | 44 | losa de 1,15 m en ~50 px |
| Techo | 56 | — |
| Pilar | 60 | — |
| Hueso | 320 | pieza de 6 cm: a 200 le caben doce píxeles y cada uno se ve como un ladrillo |
| Cráneo | 380 | — |
| Hierro (reja, antorchas) | 180 | — |
| Acero (armas del jugador) | 300 | ocupa un cuarto de la pantalla: pide más densidad |
| Madera y cuero | 260 – 380 | — |

La densidad del hueso subió de 200 a 320 **por la misma razón por la que se
añadió el tramado**: la textura de la época era de baja resolución en la PARED,
no en un objeto de seis centímetros. Lo que la delataba de cerca era el tramado,
no el tamaño de los texeles. Bajar la resolución para "verse más retro" es el
error que hace que un bicho parezca hecho de cubos pintados.

**Paleta a escalones.** `color_levels` recorta la gama. **Se cuantiza el brillo,
no cada canal por separado:** posterizar canal a canal hace que cada uno salte
en un sitio distinto y aparecen píxeles rojos y verdes donde solo debería haber
piedra. Cuantizando la luminancia y conservando el tono se obtiene el bandeado
de una paleta indexada sin la suciedad.

**Geometría de pocas caras.** Esferas de 7×4, cilindros de 4, 5 o 6 lados,
prismas donde antes había cajas. El facetado se ve y se quiere que se vea.

## 2 bis. Lo que faltaba para que fuera una PlayStation y no un posterizado

Los texeles cuadrados y la paleta a escalones daban 2001 de lejos y "filtro de
Photoshop" de cerca. Faltaban tres cosas, viven en `retro.gdshaderinc` y las
comparten todos los materiales de criatura y de arma.

**El tramado.** El escalón duro entre dos niveles de la paleta se lee como una
MANCHA, y un bicho lleno de manchas parece hecho de cubos pintados. Deshecho en
un Bayer de 4×4 se lee como superficie. Es literalmente lo que hacía la consola
con sus 15 bits de color, y es lo primero que se reconoce en una captura de la
época. Si algo "se ve muy cuadrado", esto es lo que lo arregla; bajar la
resolución de la textura solo lo empeora.

**El facetado.** `NORMAL` pasa a ser la normal de la CARA, sacada de las
derivadas de pantalla, y no la interpolada. Un modelo de pocos polígonos con
normales suaves parece una esfera mal teselada; con la normal de la cara se le
CUENTAN los triángulos, que es de lo que presumía la época. En el hueso el
fresnel del borde también usa la normal facetada: con la suave, el hilo de luz
recorrería una silueta redonda sobre un modelo anguloso y se vería el truco.

**El temblor de vértices.** La consola no tenía coma flotante en el
transformador de geometría: los vértices caían a posiciones enteras de pantalla
y por eso los modelos hierven al moverse. Aquí los vértices se pegan a una
rejilla de tamaño ANGULAR constante —así el temblor no se afina con la
distancia, igual que entonces— y solo lo llevan las criaturas y las armas. La
sillería no: sus triángulos miden metros y la misma rejilla los ondularía.

> La rejilla se aplica en espacio de VISTA tocando `VERTEX`, **nunca escribiendo
> `POSITION`**. Escribir `POSITION` desactiva el camino de paraboloide dual de
> Godot y las seis antorchas de la sala dejan de proyectar sombra bien. Costó
> descubrirlo y está en §8.

## 3. Paleta

Fría en la sombra, cálida en la luz. Todo el contraste del juego está en esa
oposición y por eso **no hay ninguna fuente de luz blanca**.

| Papel | Color | Dónde |
|---|---|---|
| Antorcha | `1.0, 0.72, 0.40` | omni de pared y de mano |
| Ascua (llama baja) | `1.0, 0.38, 0.13` | a donde va el color al ahogarse |
| Luz de arriba | `0.56, 0.68, 0.95` | tiros por las rejas del techo |
| Ambiente | `0.26, 0.33, 0.50` a 0.06 | lo justo para que el negro no sea un agujero |
| Niebla | `0.05, 0.058, 0.082` | gris azulado, nunca gris neutro |
| Piedra | `0.36, 0.35, 0.33` a `0.15, 0.14, 0.13` | — |
| Hueso | `0.50, 0.48, 0.41` a `0.20, 0.18, 0.13` | — |
| Óxido | `0.42, 0.20, 0.08` | — |

## 4. La regla de la iluminación

**La sala no se ilumina sola.** La ambiental está a 0,06 y todo lo demás lo
ponen las antorchas. Si al quitar una antorcha la zona no se queda ciega, es que
la ambiental está demasiado alta.

Seis antorchas en una sala de 29 × 29 m. Eso deja zonas enteras a oscuras a
propósito: es el pilar 3 del diseño hecho número.

**El parpadeo son tres capas de ruido, nunca un seno.** Un seno se oye: el ojo
le pilla el compás en cinco segundos y el fuego pasa a ser una bombilla con
temporizador.

1. *Temblor* — ruido rápido, ±22 % de energía.
2. *Ahogo* — ruido lento que de vez en cuando baja la llama al 60 %. Es lo que
   hace que una sala iluminada dé miedo: la luz que tienes no es una garantía.
3. *Baile* — la fuente se mueve 4 cm. Como las sombras las proyecta ella, mover
   la fuente mueve todas las sombras de la sala a la vez.

Cada antorcha arranca con una fase distinta. Sin eso, seis antorchas laten al
unísono y la sala entera pulsa como un corazón.

**La niebla volumétrica es la pieza que más devuelve.** Convierte cada antorcha
en un halo con volumen y cada reja del techo en una columna de luz. Sin ella
esto es una sala oscura; con ella es una mazmorra.

## 5. Los enemigos

El esqueleto pasó de cápsula con nariz a 24 piezas: cráneo, mandíbula, columna,
cuatro costillas de toro, esternón, clavícula, húmeros, antebrazos, manos,
pelvis, fémures, tibias, pies y un machete oxidado.

Lo que lo hace legible en una sala a oscuras no es el detalle, son dos cosas:

- **La silueta.** Cuatro costillas huecas y unas piernas finísimas se reconocen
  a quince metros con un solo píxel de ancho. Una cápsula no.
- **Las cuencas encendidas.** Son la telegrafía (§6) y además la única parte del
  bicho que se ve antes de que le llegue la luz de tu antorcha.

El shader de hueso lleva luz de borde (`rim`): con la niebla detrás, el canto
del cráneo recoge un hilo de luz y sabes que hay algo ahí sin verlo del todo. Es
la lectura que se busca: te enteras de que no estás solo antes de poder contar
cuántos son.

Las cajas se fueron. Pelvis, mandíbula, manos, pies, esternón y omóplato eran
`BoxMesh` y se leían como cubos: ahora son prismas y cilindros de cinco o seis
lados, y los huesos largos pasaron de cápsula a cilindro con salida cónica. La
silueta no cambia; lo que cambia es que a tres metros se le ven las caras.

### El paso

Las veinticuatro piezas colgaban planas de `Visual`. Ahora cuelgan de PIVOTES en
las articulaciones —cadera, rodilla, tobillo, hombro, codo, cuello— y quien las
mueve es `SkeletonRig`. No hay `Skeleton3D` ni pesos de vértice y no hacen
falta: un fémur girado sobre su propio centro se hunde en la pelvis; girado
sobre la cadera, anda.

Un ciclo de marcha son cuatro cosas y siempre las mismas:

| Pieza | Qué hace | Qué pasa sin ella |
|---|---|---|
| Piernas en oposición de fase | Se abren y se cierran | — |
| **La rodilla dobla solo en la vuelta** | Nunca en el apoyo | Anda con las piernas rígidas, y se ve a treinta metros |
| Brazos al revés que las piernas | Contrapeso | Anda como un juguete de cuerda |
| Cadera arriba y abajo dos veces por zancada | Peso | El paso flota |

**La fase del ciclo avanza con la DISTANCIA recorrida, no con el tiempo.** Es lo
único que evita que un esqueleto frenado contra una pared corra en el sitio.
Cada uno arranca con una fase distinta: sin eso, tres esqueletos de la misma
sala andan al paso como un pelotón.

**La pose se escribe a doce fotogramas por segundo.** Es la mitad del efecto: a
sesenta esto es un esqueleto procedural moderno, a doce es un enemigo de 1999.
El escalonado va en el momento de ESCRIBIR la pose, no en cada término del
cálculo; cuantizando término a término sale temblor, no fotogramas.

### La muerte

Se le apagan las cuencas y se cae a trozos, en ese orden y el orden importa: en
una sala a oscuras los ojos son lo único que se ve del bicho, así que apagarlos
ES la muerte y los huesos cayendo son la consecuencia. Al revés parece un fallo
de dibujado.

Los huesos se **reparentan** conservando su sitio en el mundo —mientras cuelguen
de la cadera, mover uno mueve a sus hijos, y un montón de huesos no tiene
hijos— y a partir de ahí cada uno cae, bota una vez y se acuesta. No es física
del motor: son treinta integraciones de Euler **a doce pasos por segundo**, que
es lo que hace que se vea el hueso saltar de una posición a la siguiente en vez
de deslizarse. Lo que estaba más alto se abre más, así que el cráneo rueda y los
pies se quedan donde estaban.

El montón se queda para siempre (`CorpseSeconds` a cero). Un esqueleto que se
desvanece deja la sala igual que estaba y no cuenta nada; los huesos por el
suelo dicen por dónde has pasado y cuánto te ha costado.

> **Trampa:** seis piezas del esqueleto están escaladas —la pelvis se ensancha,
> las costillas se aplastan, el cráneo se estira—, y una base con escala no se
> puede interpolar: al pasarla a cuaternión sale sin normalizar y el motor la
> rechaza. El derrumbe guarda giro y escala por separado.

## 5 bis. El arma del jugador

Antes se veía la antorcha y nada más. Ahora hay cuatro armas modeladas con
primitivas, cada una en su escena bajo `src/Player/Weapons/Models/`, y la elige
el propio `WeaponData`: añadir una quinta arma es soltar un `.tres` en la lista,
no tocar el controlador.

La antorcha se mudó a la mano izquierda, que es donde este documento decía que
estaba. La derecha es del arma, que es donde el jugador la espera.

**La hoja es un rombo, no una caja.** Un cilindro de cuatro lados aplastado en Z
da la sección de una hoja de verdad y sus dos vértices laterales SON el filo, así
que `edge_axis` del material cae justo donde el bisel tiene que iluminarse. Es
como se hacía una espada cuando el presupuesto eran trescientos triángulos.

**El acero lleva METALLIC bajo a propósito.** Un metal de verdad no tiene
componente difusa: solo devuelve lo que le llega, y en una mazmorra con la
ambiental a 0,06 y sin sondas de reflejo eso significa que las caras que no miran
a la antorcha salen NEGRAS. Con una hoja de cuatro caras, dos están siempre
negras y el arma se ve como una silueta recortada. Bajándolo vuelve la difusa y
con ella el material; el destello se lo queda el filo, que es donde de verdad se
ve. Además es lo que hacía la época: el metal era una textura pintada con el
brillo ya dentro, no un cálculo.

**Madera y cuero son el mismo shader.** Se distinguen por una sola cosa: hacia
dónde va el dibujo. La madera lo lleva a lo LARGO de la pieza —la fibra del
árbol— y el cuero lo lleva CRUZADO, porque es una tira enrollada. `wrap_bands` a
cero es un astil; subiéndolo es la misma pieza forrada.

### Cómo se mueve

No hay `AnimationPlayer` y no es por vagancia. Cada arma escala sus tiempos con
`SpeedScale` y el jugador puede encadenar, bloquear o esquivar a mitad de
cualquier fase: una pista grabada a 0,12 s se descuadra en cuanto el mandoble
multiplica por 1,35, y a partir de ahí el filo se ve salir cuando el golpe ya ha
pasado. La pose se calcula a partir de `PhaseProgress`, así que la animación no
puede desincronizarse del golpe: ES el golpe.

Se compone de cinco cosas que se suman y ninguna sabe de las otras:

| Capa | Qué aporta |
|---|---|
| El golpe | Un arco alrededor del HOMBRO, no del ojo |
| El paso | Un ocho tumbado: la mano baja dos veces por zancada y se escora una |
| El retraso de la vista | El arma llega un pelo tarde a donde miras. Es lo que hace que pese |
| La guardia | Se cruza delante y tapa parte de la vista, que es su precio |
| La recarga | Se sale de la mira y vuelve. Mientras, el virote no está |

Cada arma se describe con tres números y no con una tabla de poses: el PLANO del
arco (0 es tajo vertical, 90 barrido horizontal), lo que BARRE y qué parte del
arco se gasta echándose atrás. De ahí sale que la maza empuje, que la espada
barra y que el pesado del mandoble dé la vuelta entera.

> **La mentira necesaria.** El arma gira el arco entero sobre sí misma pero solo
> RECORRE una cuarta parte de él alrededor del hombro (`SwingOrbit`). Con el
> recorrido completo —que es lo que haría un brazo de verdad— un tajo de ciento
> cuarenta grados deja el arma detrás de tu propia oreja durante media animación,
> y lo que ve el jugador es que el arma desaparece justo en el fotograma en el
> que se abre la caja de golpe. Es la misma mentira que lleva contando el género
> desde 1993.

**Aquí NO se escalona la pose**, al revés que en el esqueleto. La ventana activa
del golpe ligero dura 0,10 s: a doce pasos por segundo el arma se vería salir
hasta 80 ms tarde y el jugador estaría leyendo un golpe que ya ha pasado. En el
esqueleto el escalonado es adorno; aquí sería mentir sobre los tiempos. El
parámetro está expuesto (`PoseHz`) para poder verlo puesto.

## 6. La telegrafía se mudó a los ojos

Era un cubo gris flotando sobre la cabeza. Ahora son las cuencas del cráneo, con
los mismos colores que la mira del jugador, así que la lectura vale para los dos
bandos:

| Color | Fase | Qué haces |
|---|---|---|
| Ámbar (late) | anticipación | te apartas o bloqueas |
| Morado (late) | anticipación imparable | esquivas; la guardia no sirve |
| Rojo | activo | ya es tarde |
| Azul | recuperación | tu turno |

**Solo late la anticipación.** El latido se ve mucho antes que el color: primero
lo notas por el rabillo del ojo, luego lo miras y ya distingues si es ámbar o
morado. Ese medio segundo es el combate entero.

## 7. Presupuesto de rendimiento

Medido en la máquina de desarrollo (Intel integrada, 1280 × 720, siete encuadres
de la sala de pruebas con tres esqueletos y nueve luces con sombra).

| Estado | fps |
|---|---|
| Primera versión | 17 – 40 |
| Pase de aspecto | 47 – 60 |
| **Con animación y pase de PlayStation** | **52 – 72** |

Las dos últimas filas están medidas con `tools/Perf.gd`, que recorre los siete
encuadres solo; la de 47 – 60 se midió a mano y no es del todo comparable. Lo que
sí es comparable es el A/B del pase retro, hecho con la misma herramienta en la
misma sesión:

| Tramado, facetado y temblor | fps |
|---|---|
| Apagados | 58 – 75 |
| **Puestos** | **52 – 72** |

Unos cuatro fotogramas, un 8 %. Lo caro de los tres es el temblor de vértices,
que obliga a invertir la matriz de modelo-vista por vértice; el tramado y el
facetado son dos instrucciones cada uno. La animación del esqueleto y el arma no
aparecen en la medida: son trece nodos y un puñado de senos por bicho, y encima
la pose solo se escribe doce veces por segundo.

De dónde salió, por orden de lo que más dio:

| Cambio | Por qué |
|---|---|
| Sombras omni a paraboloide dual | Una sombra de cubo son **seis** pasadas por luz y fotograma. Seis antorchas pasaron de 36 pasadas a 6. La distorsión no se ve con luz temblorosa a media distancia |
| Fuera el SSAO | Costaba casi tanto como la niebla volumétrica y el shader ya oscurece las juntas, que es donde se notaba |
| Rejilla de niebla 40×24 | La resolución por defecto es lujo para una sala cerrada |
| Render 3D al 80 % | 1024×576 escalado. No es solo rendimiento: tiene el grano justo de la época |
| Menos polvo en el aire | Cuadros transparentes **con luz**: mucho relleno solapado para lo poco que aportan |
| Saltar planos del triplanar | Un plano con peso despreciable cuesta igual que uno que se ve. En geometría alineada a ejes casi siempre manda uno |

El shader de piedra **no** era el cuello de botella: optimizarlo de 430 a 56
hashes por píxel apenas movió la cifra. Lo caro eran las sombras y los efectos de
pantalla. Conviene medir antes de optimizar.

## 8. Trampas encontradas

- **`Transform3D` en `.tscn` se serializa por FILAS de la base, no por
  columnas.** Escribir las rotaciones por columnas las deja transpuestas, o sea
  giradas al revés. Costó dos focos apuntando al techo, dos antorchas metidas
  dentro del muro y un esqueleto con el machete a la espalda.
- **Caras coplanares en CSG.** Un pilar cuya cara superior está exactamente en
  el plano de la cara inferior del techo deja artefactos. Se resuelve enterrando
  cada pieza en la losa vecina, y separando el techo a su propio
  `CSGCombiner3D`.
- **Un cuadro de llama delante de su propia luz proyecta una sombra enorme.** La
  luz va **por delante** del cuadro, no detrás.
- **Escribir `POSITION` en el shader de vértices rompe las sombras de las
  antorchas.** Godot mete la transformación de paraboloide dual JUSTO antes de
  `gl_Position`, y `POSITION` la pisa. Con las seis antorchas en modo paraboloide
  —que es de donde salió la mitad del rendimiento— cualquier malla que escriba
  `POSITION` proyecta una sombra sin sentido. El temblor de vértices se hace
  tocando `VERTEX` en espacio de vista, que deja al motor hacer su proyección.
- **`Basis.Slerp` exige una base ortonormal.** Media docena de piezas del
  esqueleto están escaladas, y una base con escala pasada a cuaternión sale sin
  normalizar: el motor lanza `Quaternion is not normalized` y el hueso se queda
  clavado. Giro y escala se guardan por separado y se vuelven a juntar al
  escribir la transformación.
- **Un `NodePath` exportado sin valor llega NULO, no vacío.** `Ammunition.IsEmpty`
  revienta en `_Ready`. Se compara con patrón (`is { IsEmpty: false }`) o se
  inicializa a `new()`.
- **Una textura procedural no tiene mipmaps.** Cuando un texel baja de un píxel
  el ruido fino deja de ser detalle y pasa a ser centelleo. Se mide con
  `fwidth()` y se apaga el detalle, dejando un suelo del 30 % para que las
  superficies muy rasantes no se queden lisas.

## 9. Herramientas

Dos escenas de desarrollo que no forman parte del juego y se pueden borrar sin
que se entere nadie. Están porque afinar una pose a ciegas es imposible y con
esto se ve el resultado en veinte segundos.

| Herramienta | Qué hace |
|---|---|
| `godot --path . tools/Rig.tscn` | Un esqueleto solo, con luz plana y cámara de perfil que lo sigue. Guarda el ciclo de marcha, el ataque y el derrumbe fotograma a fotograma en `user://` |
| `godot --path . tools/Capture.tscn` | La sala de pruebas de verdad: recorre las cuatro armas golpeando, bloquea, esquiva y mata a un esqueleto, guardando capturas en `user://` |
| `godot --path . tools/Perf.tscn` | Los siete encuadres de §7 y el rango de fps |

`Capture.gd` baja `Engine.time_scale` durante los golpes. No es capricho:
guardar un PNG cuesta décimas de segundo REALES y el delta del motor es tiempo
real, así que sin frenar el reloj entre captura y captura se va medio golpe.

## 10. Lo que falta

- **Las manos del jugador.** Se ven las armas flotando: no hay brazos. Es
  trabajo de M7 y está anotado como decisión abierta en `DISENO.md` §13.
- **Sonido.** Media atmósfera de una mazmorra es el goteo y el eco, y aquí no
  hay ni una muestra.
- **El arma atraviesa las paredes.** Lo estándar es un segundo `Viewport` con
  su propia cámara; la antorcha ya tenía el mismo problema.
- **El esqueleto no reacciona al golpe.** Anda, ataca y se cae a trozos, pero
  entre medias encaja los impactos sin inmutarse. Un respingo de dos fotogramas
  es lo que dice si le has dado.
- **El interior del hueco del techo se quema.** La cara de fondo de la reja sale
  a blanco puro. Se arregla con un material propio más oscuro para el brocal.
- **El hueso se quema al lado de la antorcha.** A menos de metro y medio el
  esqueleto sale a blanco. O baja `bone_pale`, o la antorcha de mano necesita
  una caída más agresiva de cerca.
