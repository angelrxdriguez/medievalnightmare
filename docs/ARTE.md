# Dirección de arte — prueba de aspecto

> **Esto se salta la norma de `DISENO.md` §11:** "hasta M6 no se toca ni un solo
> asset de arte". Se hizo a propósito y por encargo, para ver a dónde puede
> llegar el juego antes de comprometerse. **No es permiso para seguir haciendo
> arte.** El siguiente hito sigue siendo M2, en gris o con esto puesto, da igual:
> lo que decide si el juego sigue es si se puede perder una sala con tres
> esqueletos, no cómo se ve.
>
> Lo que sí sirve de aquí: la paleta, los números de iluminación, el presupuesto
> de rendimiento medido y la RECETA del esqueleto (§5). Eso se conserva aunque el
> arte final se rehaga entero en M7.

---

## 1. La regla

**Nada de imágenes.** No hay un solo PNG en el proyecto y no lo va a haber
hasta M7. Toda la textura sale de siete shaders procedurales —piedra, hueso,
hierro, llama, acero, mango y tela— sobre una caja de herramientas común
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
| Chapa (yelmo, hombrera, cinturón) | 95 | pieza grande y lisa: más densidad la vuelve sal y pimienta |
| Harapo | 150 | la trama va con el paño, no con el mundo |
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
| Hueso | `0.44, 0.41, 0.34` a `0.18, 0.15, 0.11` | bajó un 12 % y se fue a cálido cuando se midió EN LA SALA: con seis antorchas sumando, el hueso claro salía blanco de plástico contra la piedra |
| Tela podrida | `0.31, 0.26, 0.18` a `0.12, 0.10, 0.08` | siempre por debajo del hueso: si compite, se lleva la mirada el faldón |
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

El esqueleto pasó de cápsula con nariz a 24 piezas, y de 24 a 47. Lo segundo no
fue añadir detalle: fue cambiar tres cosas que ninguna cantidad de textura
arregla —las proporciones, la caja torácica y la cara— y ponerle encima lo poco
que hace falta para que se lea como un guerrero muerto y no como una lámina de
anatomía.

**No se buscó realismo y se rechazó a propósito.** Un cráneo con arcos
cigomáticos y treinta y dos dientes en un bicho que vas a ver a cuatro metros y
medio segundo es trabajo tirado, y encima delata la época equivocada: lo que
hacía 2001 era elegir CUATRO formas y exagerarlas. Aquí las cuatro son la
cuenca, la costilla, el faldón y el yelmo.

### Lo que lo cambió, por orden de lo que más dio

**Las proporciones.** Antes era una figura de 1,80 con medidas de persona: piernas
largas, hombros estrechos, cráneo pequeño. Se leía como un maniquí. Ahora las
piernas ocupan el 45 % de la altura en vez del 48, los hombros pasaron de 37 a
45 cm, el cráneo creció un 15 % y el torso va inclinado 9° hacia delante con la
cabeza deshaciendo la inclinación por debajo. El bicho pasa de pasear a ir a por
ti sin haber tocado una sola animación.

**La caja torácica.** Cuatro aros horizontales se leen como aros: de frente son
cuatro barras y de lado son cuatro circunferencias, y en las dos vistas parece
una persiana. Ahora son cinco, giradas 15° para que el frente CAIGA —que es
hacia donde van las costillas de verdad— y aplastadas al 72 % en profundidad. La
misma pieza, dos números distintos, y de pronto hay un pecho.

**Las cuencas.** Eran dos esferas encendidas pegadas a la cara. Ahora son dos
esferas DENTRO de un aro, o sea dentro de un agujero, y el agujero se lee incluso
apagado. Es la diferencia entre un bicho con dos luces y una calavera.

### El equipo, que es lo que lo hace medieval y no un esqueleto de museo

Tres piezas de chapa oxidada y cuatro paños. Ninguna es armadura: es lo que no se
llevó el que lo despojó.

| Pieza | Qué hace por la silueta |
|---|---|
| Yelmo | Le da una línea recta y oscura arriba. Un cráneo pelado es una bola; con el casquete hay una cabeza |
| Barrote nasal | Parte la cara en dos. A cuatro metros ya no es un barrote, es la sombra que separa las dos cuencas |
| Hombrera | Ensancha un hombro y solo uno. La asimetría es lo que hace que no parezca un icono |
| Faldón de harapos | Llena el vacío entre la pelvis y las rodillas, que es donde se le veía el truco |

El faldón trajo el séptimo shader (`rag.gdshader`) y trajo lo mejor de todo el
pase: **el recorte de un bit**. El bajo del harapo no es una línea, se muerde con
ruido evaluado en una rejilla GRUESA, y cada píxel está o no está. Es lo único
que sabía hacer la consola —no tenía alfa por píxel— y un borde dentado de
píxeles cuadrados se reconoce como de la época antes de que te dé tiempo a mirar
nada más. Con transparencia de verdad esto sería una cortina de un juego de
ahora.

### La semilla por pieza

Cuarenta y siete huesos con el mismo material y la misma fórmula son cuarenta y
siete veces el mismo hueso, y el ojo lo pilla enseguida: se lee como una textura
repetida y no como un montón de huesos. Los tres shaders de criatura llevan un
`instance uniform piece_seed` que `SkeletonRig` reparte al arrancar, sacado del
NOMBRE del nodo —así el fémur izquierdo tiene siempre la misma mancha— más un
desplazamiento por bicho, que es lo que evita que tres esqueletos de la misma
sala sean tres copias.

### La luz a escalones

`bone.gdshader` y `rag.gdshader` se quedan con la luz (`light()`), y sale a
cuenta por dos cosas:

- **El techo.** Una antorcha a metro y medio le mete al hueso cinco veces la
  energía que necesita y el bicho se va a blanco puro justo cuando lo tienes
  encima. Estaba anotado como fallo pendiente. La salida fácil era bajar
  `bone_pale`, que arregla el primer plano y deja apagado el resto de la sala; lo
  que hay que comprimir es la ENERGÍA: `e / (1 + e·k)` deja la luz baja como
  estaba y aplasta la alta contra un tope. Es lo que hacía una tabla de luz de
  ocho bits.
- **Los escalones.** Un degradado suave sobre un modelo de cuarenta caras es lo
  que más delata que esto es de ahora. Cinco niveles, deshechos con el MISMO
  Bayer que la paleta —si no, vuelven las manchas— y el hueso se sombrea como se
  pintaba entonces.

La envolvente (`light_wrap`, 0,35) no es adorno: con el terminador duro, un
esqueleto iluminado de lado se parte en dos mitades y la oscura desaparece contra
el fondo, que es justo lo que no se quiere de un bicho cuya lectura es la
silueta.

### Lo que sigue haciéndolo legible

Sigue sin ser el detalle, y siguen siendo dos cosas:

- **La silueta.** Cinco costillas huecas, un faldón oscuro y unas piernas
  finísimas se reconocen a quince metros con un solo píxel de ancho. Una cápsula
  no.
- **Las cuencas encendidas.** Son la telegrafía (§6) y además la única parte del
  bicho que se ve antes de que le llegue la luz de tu antorcha.

El shader de hueso lleva luz de borde (`rim`): con la niebla detrás, el canto del
cráneo recoge un hilo de luz y sabes que hay algo ahí sin verlo del todo. Es la
lectura que se busca: te enteras de que no estás solo antes de poder contar
cuántos son.

Las cajas se fueron hace dos pases. Pelvis, mandíbula, manos, pies, esternón y
omóplato eran `BoxMesh` y se leían como cubos: son prismas y cilindros de cinco o
seis lados, y los huesos largos pasaron de cápsula a cilindro con salida cónica.
Los pies se dieron la vuelta en este pase: el prisma apuntaba con el vértice
hacia DELANTE y de frente eran dos púas. Un pie se estrecha por el talón.

### El paso

Las veinticuatro piezas colgaban planas de `Visual`. Ahora cuelgan de PIVOTES en
las articulaciones —cadera, rodilla, tobillo, hombro, codo, cuello, mandíbula— y
quien las mueve es `SkeletonRig`. No hay `Skeleton3D` ni pesos de vértice y no
hacen falta: un fémur girado sobre su propio centro se hunde en la pelvis;
girado sobre la cadera, anda.

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
sesenta esto es un esqueleto procedural moderno, a doce es un enemigo de 1999. El
escalonado va en el momento de ESCRIBIR la pose, no en cada término del cálculo;
cuantizando término a término sale temblor, no fotogramas.

### El golpe y el respingo

La mandíbula cuelga floja —un muerto no aprieta los dientes—, se ABRE durante la
anticipación y se cierra de golpe en el fotograma en que sale el filo. Es la
telegrafía de las cuencas contada otra vez con la forma, y sirve para lo mismo:
por el rabillo del ojo ves que algo se abre antes de distinguir de qué color es.

El torso se echa ATRÁS al levantar el machete y se tira hacia DELANTE al soltarlo.
Estaba al revés —se inclinaba hacia delante para cargar y se enderezaba al
golpear— y por eso la anticipación no pesaba: un tajo se carga echándose atrás.

Y encaja los golpes. Un respingo de 0,18 s, que a doce fotogramas por segundo son
dos y pico: el torso atrás, la cabeza descolgada, la cadera cede un dedo y la
mandíbula se abre. **No interrumpe el ataque a propósito**: el esqueleto se
compromete igual que el jugador y un golpe a tiempo no cancela el suyo. Sin esto
le pegas cuatro veces seguidas y sigue andando hacia ti sin enterarse, y lo que
lee el jugador no es "es duro" sino "no le he dado", que es lo peor que puede
pasar en un combate cuyo golpe ligero dura 0,10 s.

### La muerte

Se le apagan las cuencas y se cae a trozos, en ese orden y el orden importa: en
una sala a oscuras los ojos son lo único que se ve del bicho, así que apagarlos
ES la muerte y los huesos cayendo son la consecuencia. Al revés parece un fallo
de dibujado.

Los huesos se **reparentan** conservando su sitio en el mundo —mientras cuelguen
de la cadera, mover uno mueve a sus hijos, y un montón de huesos no tiene
hijos— y a partir de ahí cada uno cae, bota una vez y se acuesta. No es física
del motor: son cuarenta y siete integraciones de Euler **a doce pasos por
segundo**, que es lo que hace que se vea el hueso saltar de una posición a la
siguiente en vez de deslizarse. Lo que estaba más alto se abre más, así que el
cráneo rueda y los pies se quedan donde estaban. El yelmo se le cae y rueda
aparte, que es de las cosas que más se miran del montón.

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

### El esqueleto de 47 piezas

No se ha podido medir contra las cifras de arriba: se hizo en otra máquina, con
GPU dedicada, y ahí la sala entera va sobrada. Lo que sí se hizo fue el A/B en
esa máquina, con la misma herramienta, en la misma sesión y guardando el trabajo
en un `stash` para medir el esqueleto viejo:

| Esqueleto | fps (GPU dedicada, sin vsync) |
|---|---|
| 24 piezas | 548 – 913 |
| **47 piezas** | **530 – 984** |

Los rangos se solapan encuadre a encuadre y en tres de los siete el nuevo sale
POR ENCIMA del viejo. Y hay algo peor para la medida: repitiendo la misma toma
con el MISMO binario un rato después salió 858 – 1366. **La variación entre
ejecuciones es varias veces mayor que la diferencia entre los dos esqueletos**,
así que lo único que se puede afirmar es que veintitrés mallas más por bicho no
se ven en esta máquina.

Eso no dice que sea gratis en la Intel integrada —ahí lo que manda es el coste de
enviar dibujados, y con tres esqueletos y seis antorchas con sombra son 33 × 3 × 6
pasadas solo de sombra— sino que **está sin medir donde importa y hay que volver
a medirlo antes de dar el presupuesto por bueno**.

Lo que sí se hizo, por si acaso: las catorce piezas de detalle —cuencas, dientes,
pómulos, vértebras, nariz, pomo y los cuatro harapos— llevan `cast_shadow`
apagado. Están metidas dentro de piezas más grandes o pegadas a ellas y su sombra
no aporta nada, pero se pagarían seis veces cada una.

> La herramienta mentía. `tools/Perf.gd` promediaba
> `Engine.get_frames_per_second()` cuarenta veces seguidas, y ese contador se
> actualiza UNA VEZ POR SEGUNDO: a 500 fps se leía cuarenta veces el mismo número
> y los siete encuadres salían idénticos hasta el decimal. Ahora cronometra 120
> fotogramas con `Time.get_ticks_usec()` y apaga el vsync, sin el cual cualquier
> máquina holgada marca 60 clavados y la medida no dice nada.

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
- **En `light()` no se multiplica por `ALBEDO`, y `LIGHT_COLOR` trae un PI de
  más.** El motor multiplica `DIFFUSE_LIGHT` por el albedo DESPUÉS de llamar a la
  función, así que hacerlo también dentro lo eleva al cuadrado; y `LIGHT_COLOR`
  llega como color × energía × PI porque el 1/PI vive dentro de las BRDF físicas.
  Con las dos juntas el esqueleto salió a un tercio de la luz que le tocaba y
  parecía que la culpa era del techo de energía recién estrenado.
- **Las cuencas se quedan flotando al morir.** No son huesos: son una fuente de
  luz y no se reparentan con el montón, así que apagarlas las deja donde estaba
  la cabeza. Apagadas siguen siendo dos esferas negras a metro y medio del suelo.
  Al terminar de apagarse hay que ESCONDER el nodo, que además se lleva la luz.
- **La condición de reposo del derrumbe no se cumplía nunca.** Comparaba la
  velocidad de llegada al suelo contra medio metro por segundo, y esa velocidad
  ya lleva sumada la gravedad del paso: a doce pasos por segundo son 1,08 m/s de
  caída por paso, así que la pieza no se posaba jamás. No se veía —quedaba
  clavada a su altura de reposo dando botes de un milímetro— pero como no llegaba
  a posarse tampoco llegaba a ACOSTARSE, y una docena de huesos se quedaba en el
  montón con la orientación con la que había caído, incluido el machete de pie.
  Se mira lo que devuelve el suelo (`CollapseSettle`), no lo que traía la pieza.
  Lo cazó una comprobación de tres líneas en `tools/Rig.gd` que lista las piezas
  que se han quedado de canto, y ahí sigue.
- **`QuadMesh` mira a +Z.** Los cuatro paños del faldón se colocaron alrededor de
  la cadera sin girarlos y los tres que no daban a +Z quedaron iluminados por
  detrás, o sea negros. No hace falta tocar `FRONT_FACING`: se giran los paños
  para que su normal mire hacia fuera y se acabó.

## 9. Herramientas

Tres escenas de desarrollo que no forman parte del juego y se pueden borrar sin
que se entere nadie. Están porque afinar una pose a ciegas es imposible y con
esto se ve el resultado en veinte segundos.

| Herramienta | Qué hace |
|---|---|
| `godot --path . tools/Rig.tscn` | Un esqueleto solo. Hace dos RETRATOS de siete tomas —cuatro lados, primer plano de la cabeza, torso y a once metros—, uno con luz de estudio y otro con la luz del juego, y luego el ciclo de marcha, el ataque, el respingo y el derrumbe. Todo a `user://` |
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
- **El interior del hueco del techo se quema.** La cara de fondo de la reja sale
  a blanco puro. Se arregla con un material propio más oscuro para el brocal.
- **El fémur atraviesa el harapo al andar.** El faldón cuelga de la cadera y no
  sabe nada de las piernas, así que en la zancada larga el muslo lo cruza. Se
  puede tapar estrechando los paños o darle al paño delantero un giro con la
  fase del paso; de momento se deja, que atravesarse era rutina en la época.
- **El presupuesto de §7 está sin medir con el bicho nuevo.** El A/B se hizo en
  una máquina con GPU dedicada y ahí no se nota; hace falta repetirlo en la
  Intel integrada antes de dar por bueno el rango de 52 – 72 fps.
- **Los otros dos enemigos no existen.** El ogro y el jefe siguen siendo una
  línea en `DISENO.md`. Lo de aquí —proporciones exageradas, cuatro formas
  elegidas, semilla por pieza y luz a escalones— es la receta y debería
  aplicarse tal cual cuando les toque, no reinventarse.
