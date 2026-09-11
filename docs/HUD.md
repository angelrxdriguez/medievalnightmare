# HUD — diseño básico y por dónde pueden ir los tiros

> **Estado: diseñado, no hecho.** El hito del HUD es M8 (`DISENO.md` §11). Esto
> se escribe ahora porque al cerrar M2 se añadió la rotura de guardia y no hay
> forma de leerla, no porque toque construirlo.
>
> Lo que sale de aquí que es **decisión** está en §4 y §5. Lo que sale de aquí
> que es **opción abierta** está en §6, marcado como tal. Si algo de §6 acaba
> entrando, se sube a `DISENO.md` y deja de ser una opción.

---

## 1. La regla

**El HUD solo enseña lo que no se puede leer mirando al mundo o a tus propias
manos.** Ese es el filtro entero. Si algo se puede contar con el arma, con el
borde de la pantalla o con lo que pasa delante de ti, no es HUD: es diseño de
arte, y va ahí.

Sale de tres sitios del documento de diseño, y ninguno es negociable:

- §5: *"No hay barra de vida ni números en pantalla: la vida se lee mirando el
  borde."* Lo que vale para la vida vale para todo lo demás.
- Pilar 3: *la oscuridad es una mecánica.* Cada elemento encendido en pantalla
  es luz que el jugador tiene garantizada pase lo que pase. Un HUD grande es una
  linterna que no se apaga.
- `ARTE.md` §1: **nada de imágenes.** El HUD se dibuja con primitivas (`_Draw`),
  igual que la mira. No hay un solo PNG y no lo va a haber. La tipografía es
  para los menús, no para el combate.

**Corolario incómodo:** casi todo lo que un HUD suele enseñar, aquí no entra. Eso
es intencionado, no falta de ambición.

## 2. Lo que ya es HUD y no lo parece

Antes de añadir nada conviene mirar lo que hay, porque es más de lo que parece y
ya cubre lo importante.

| Qué | Dónde | Qué cuenta |
|---|---|---|
| **La mira** | `src/Ui/Crosshair.cs` | Dónde apuntas y **en qué fase de tu golpe estás**, con los colores de la telegrafía enemiga |
| **La viñeta** | `src/Ui/DamageVignette.cs` | La vida: más roja cuanto menos te queda, y late por debajo del 40 % |
| **El arma en la mano** | `src/Player/Weapons/WeaponView.cs` | Si andas, si estás en guardia, si recargas, si esquivas, si te acaban de abrir la guardia |
| **Las cuencas del esqueleto** | `ARTE.md` §6 | La telegrafía del enemigo: ámbar, morado, rojo, azul |

**Esto ya es el 80 % del HUD y no ocupa un píxel de esquina.** Lo que falta es
poco, y hay que resistirse a que sea mucho.

## 3. Lo que falta, y por qué falta

| Hueco | Se nota cuando | ¿Hace falta de verdad? |
|---|---|---|
| **Guardia rota** | Pulsas bloquear durante 10 s y no pasa nada | **Sí.** Hoy se lee como un fallo del juego, no como un estado |
| **Carga de la guardia** | Vas a recibir el segundo golpe y no lo sabes | **Sí.** Sin esto, romperse es un castigo sin aviso |
| **Virotes** | Te quedan dos y no hay forma de saberlo | **Sí**, pero solo con la ballesta en la mano |
| **Enfriamiento de la esquiva** | Esquivas y no sale | **Puede que no.** Son 2 s: se aprenden con el pulgar, no con los ojos |
| **Amuleto y su enfriamiento** | — | Sí, pero no existe como objeto hasta M3 |
| **Qué llevas encima** | Al decidir si extraer | **Sí, y es el que más importa.** Es el pilar 2 y hoy no tiene nada. M3 |

## 4. El HUD básico

Cuatro cosas, tres de ellas ya construidas. Nada en la mitad superior de la
pantalla, que es por donde entra lo que te va a matar.

```
 +--------------------------------------------------------+
 |#                                                      #|  <- viñeta: la vida
 |                                                        |
 |                                                        |
 |                           |                            |
 |                        ---+---                         |  <- mira: fase + guardia
 |                           |                            |
 |                                                        |
 |                                                        |
 |   <>                                         | | | |   |  <- amuleto (M3)  virotes
 |#                                                      #|
 +--------------------------------------------------------+
```

**La guardia se lee en la mira.** No en una barra, no en una esquina: en la
cruz, que ya es el sitio donde el jugador lee su propio estado de combate y el
único que está mirando cuando le están pegando.

| Estado | Qué hace la mira | Por qué |
|---|---|---|
| Guardia baja | Cuatro brazos, hueso al 55 % | Es la mira de siempre |
| Guardia alta | Los brazos se acercan al centro | La cruz se cierra: es un escudo, se ve que estás tapado |
| Cargada | Los brazos se tiñen de ámbar según la carga (0 → 1) | Ámbar ya significa *"esto va a pasar"* en los dos bandos |
| **Rota** | Los brazos se separan de golpe y se apagan | Lo contrario de cerrarse. No hace falta explicarlo |
| Volviendo | Los brazos vuelven al centro **durante los 10 s** | **La recuperación ES la animación.** Sin número, sin barra, sin texto |

Esa última fila es la pieza de la que cuelga todo lo demás: los diez segundos de
castigo se cuentan solos, con el mismo gesto que ya usa la mira, y el jugador
sabe cuándo vuelve a tener guardia sin haber leído nada.

**Los virotes son seis muescas**, abajo a la derecha, y **solo con la ballesta en
la mano**. Aparecen al sacarla y se van dos segundos después; vuelven a aparecer
cuando el número cambia. Seis muescas se cuentan de un vistazo; un `4 / 6` se
lee, que es más lento y además es un número en pantalla.

**El amuleto es un rombo** abajo a la izquierda, que **se rellena según vuelve**
—lleno es disponible— y desaparece en cuanto lo está. No entra hasta M3, cuando
el amuleto exista.

> Hay un borrador interactivo de todo esto, con la mira funcionando sobre la sala
> a oscuras y a 1280 × 720 reales. Es una maqueta en HTML, no código del juego.

## 5. Reglas que valen para cualquier elemento

1. **Ausente por defecto.** Si no está pasando nada, la pantalla está vacía menos
   la mira. Todo lo demás entra cuando cambia y se va cuando deja de importar.
2. **Nada de texto en combate.** Ni números, ni etiquetas, ni avisos. El
   `DebugHud` es una herramienta y muere en M8 (ya lo dice su propio comentario).
3. **Los colores son los de la telegrafía**, los mismos para los dos bandos:
   ámbar es anticipación, morado es imparable, rojo es que ya es tarde, azul es
   tu turno. El HUD no inventa un idioma nuevo.
4. **Nada blanco.** `ARTE.md` §3: no hay una sola fuente de luz blanca en el
   juego, y el HUD no va a ser la primera. El color base es el hueso
   (`0.92, 0.90, 0.84`) a opacidad baja.
5. **Nada en la mitad de arriba**, y nada a menos de un 4 % del borde.
6. **Se redibuja al cambiar de estado, nunca por fotograma.** La mira ya lo hace
   así y el motivo está escrito en su código: son cuatro líneas, pero repintarlas
   para nada es justo el gasto que no se ve.
7. **Se diseña sobre la sala oscura, no sobre gris.** Un HUD ajustado sobre fondo
   plano se vuelve invisible en penumbra o quema la escena entera.

## 6. Por dónde pueden ir los tiros

Esto **no** es la versión 1.0. Son las opciones que se han considerado, con lo
que cuestan, para que la discusión ya esté hecha cuando toque M8.

### 6.1. La esquiva: probablemente nada

Son 2 s. La opción barata es no enseñarla y ver si alguien la echa de menos. Si
hace falta, lo mínimo que funciona es **un arco fino bajo la mira que se cierra**
en esos 2 s. Lo que no se puede hacer es meterle color: ámbar, rojo y azul ya
están cogidos por la fase del golpe, y la mira no puede contar dos cosas con el
mismo idioma.

### 6.2. La vida: el borde se queda, pero se le puede añadir el pulso

La viñeta funciona y no se toca. Si al jugar salas largas resulta que el borde
rojo se normaliza y deja de leerse, hay dos salidas **diegéticas** antes que una
barra:

- **El arma tiembla** por debajo del 25 %. Cuesta poco: `WeaponView` ya compone
  poses por peso, es una más.
- **La respiración se oye.** Es M7, pero es la que más devuelve: el jugador deja
  de mirar la pantalla para saber cómo está.

Una barra de vida es la última opción y contradice §5 del diseño. Si se llega a
eso, lo que hay que revisar es la viñeta, no añadir una barra al lado.

### 6.3. Lo que llevas encima: el hueco grande, y es de M3

El pilar 2 es *"salir vivo vale más que seguir"*, y esa decisión hoy no tiene
nada en pantalla. Tres formas de darle soporte, de más barata a menos:

1. **Nada en el HUD; el inventario se abre y se mira.** La decisión se toma con
   el inventario delante, en la sala de extracción, que es donde se toma de
   todas formas. Es coherente con el resto del documento.
2. **Un contador de huecos llenos** al abrir la sala de extracción. Un número,
   pero solo ahí y solo ese rato.
3. **La sala de extracción se anuncia sola** —luz, sonido, una puerta que se ve
   distinta— y el HUD no interviene. Es lo que más encaja con el pilar 3 y lo
   que más trabajo de nivel pide.

**La 1 es la recomendación** y la 3 es arte de nivel, no HUD. La 2 solo si al
jugar M3 la decisión de extraer se toma a ciegas.

> **Hecha en M3 (11-09-2026): la 1.** El inventario se abre con I, para el juego
> y enseña lo que llevas puesto, lo que llevas encima y —solo delante del
> arcón— lo guardado. Es un menú, no HUD: por eso puede tener letras. Durante el
> combate no ha entrado nada nuevo en pantalla.

### 6.4. La antorcha, si acaba consumiéndose

`DISENO.md` §13 tiene abierta la pregunta de si la antorcha se gasta. **Si se
gasta, no se cuenta en el HUD: se cuenta con la llama**, que baja y se va a color
ascua (`1.0, 0.38, 0.13`, `ARTE.md` §3). El jugador no necesita saber cuántos
segundos le quedan; necesita notar que la sala se está oscureciendo. Es una razón
más para que la respuesta a esa pregunta sea "sí".

### 6.5. Daño recibido por la espalda

Se descartó al jugar M2: morir por detrás **se siente el precio de la cámara**
(`DISENO.md` §12), así que un indicador direccional de daño está fuera. Queda
apuntado aquí para que no vuelva a proponerse: sería quitarle al jugador el
trabajo que el pilar 3 le da a propósito.

## 7. Lo que NO va en el HUD

La parte más útil de esta página.

- Barra de vida, número de vida, corazones, cualquier cosa que cuente la vida
  que no sea el borde.
- Barra de estamina. No hay estamina.
- Minimapa, brújula, marcadores de objetivo, flechas.
- Retrato del personaje. No te ves: esa es la cámara.
- Barras de vida sobre los enemigos. Lo que un enemigo va a hacer se lee en sus
  cuencas; lo que le queda, en que se cae.
- Números de daño flotantes.
- Contador de enemigos, de sala, de tiempo, de puntuación.
- Diario de misiones, avisos de objeto conseguido, tutoriales en pantalla.
- Iconos de estado, debuffs, alteraciones.

## 8. Cuándo se hace

**No ahora.** El orden es el del plan de trabajo: M3, M4, M5, M6 y luego arte.
Lo único de esta página que se adelanta es lo que ya está hecho —el arma sale
despedida al romperse la guardia y se queda caída mientras dura— porque sin eso
la rotura no se entiende al jugar, y M2 se cierra jugando.

Cuando llegue M8, el orden dentro del HUD es: la mira con la guardia (§4)
primero, los virotes después, y el amuleto cuando exista. Lo demás se decide
entonces con esta página delante.
