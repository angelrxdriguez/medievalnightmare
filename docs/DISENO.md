# Medieval Nightmare — Diseño de la versión 1.0

Este documento describe **la versión 1.0 completa**. Todo lo que no está aquí,
no está en el juego. Si algo se te ocurre y no aparece en este documento, la
respuesta por defecto es no.

---

## 1. Qué es

Un juego de combate medieval en tercera persona, de fantasía oscura, con
gráficos poligonales de principios de los 2000. Bajas a mazmorras a por
equipo, y cada sala que avanzas aumenta lo que puedes ganar y lo que puedes
perder. Si mueres, pierdes todo lo que llevabas encima.

Tres niveles, un jefe final, 2-3 horas.

## 2. Pilares

Tres. Si una decisión no sirve a uno de estos tres, no entra.

1. **El riesgo lo eliges tú.** Los golpes rápidos son seguros y hacen poco.
   El pesado te clava en el sitio casi dos segundos y mata de uno. Entre esas
   dos opciones está todo el combate.
2. **Salir vivo vale más que seguir.** La tensión no está en el combate, está
   en la decisión de avanzar una sala más o extraer con lo que ya tienes.
3. **La oscuridad es una mecánica.** La niebla y la falta de luz no decoran:
   son lo que te impide ver qué te va a matar.

## 3. Referencias

- **Blade of Darkness (2001)** — la época, la estética y el cuerpo a cuerpo
  en tercera persona. La referencia principal.
- **Dark Messiah of Might & Magic** — el ritmo: ágil, pero con golpes cargados
  que te dejan vendido mientras salen.
- **Dark and Darker** — la decisión de extraer.
- **Hexen** — la paleta.

**En qué NO se parece:** no es un Souls (no hay estamina, ni parry, ni esquiva
con invulnerabilidad), no es un King's Field (se probó el combate lento en M1
y no funcionaba), no tiene PvP, no es mundo abierto.

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
- **El ligero es la base.** Rápido, encadenable con 0,25 s de margen de
  entrada, y te deja moverte al 55 % de la velocidad de marcha.
- **El pesado es la apuesta.** Hace 2,5× de daño y te deja clavado en el sitio
  1,65 s. Solo puedes girar durante la anticipación. Ahí es donde te pegan.
- **Fijado de objetivo:** blando. La cámara sigue al enemigo; tú te mueves
  libre. Se puede quitar en cualquier momento. *Sin implementar.*
- **Bloqueo:** reduce el daño recibido un 80 %. No hay parry. *Sin implementar.*
- **Paso lateral:** desplazamiento corto de 0,4 s. **No da invulnerabilidad.**
  *Sin implementar.*
- **Vida del jugador:** 100. No se regenera.
- **Pociones:** curan 40. Máximo 3 por incursión, ocupan hueco de inventario.

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

**Armas (3).** Comparten el mismo esqueleto de animación; cambian velocidad,
alcance y daño. Es lo que las hace baratas de producir.

| Arma | Daño | Alcance | Velocidad | Golpes para matar un esqueleto |
|---|---|---|---|---|
| Espada corta | 12 | 1,8 m | 0,8 | 4 ligeros (1,5 s) o 2 pesados |
| Maza | 18 | 1,6 m | 1,0 | 3 ligeros (1,2 s) o 1 pesado |
| Mandoble | 30 | 2,6 m | 1,35 | 2 ligeros (0,9 s) o 1 pesado |

Las tres matan en un tiempo parecido: se diferencian por alcance y por ritmo,
no por ser mejores o peores.

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

**Ogro** — lento y devastador. Dos golpes te matan. Enseña a usar el espacio
y a no ser codicioso. Vida 180, daño 55.

**El Guardián del Pozo (jefe)** — un ogro coronado que invoca esqueletos.
Reutiliza dos enemigos existentes, que es exactamente por qué es el jefe.
Vida 400.

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
| M0 | Controlador en tercera persona | ✅ Camino, salto y caigo en una sala CSG |
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

## 12. Riesgos

| Riesgo | Cuándo se ve | Qué hacemos |
|---|---|---|
| ~~El combate lento resulta aburrido~~ | M1 | **Ocurrió.** Se cambió a ligero rápido + pesado comprometido |
| Sin bloqueo ni esquiva, pelear contra 2+ enemigos es una carrera de daño | M2 | Implementar el paso lateral, que ya está definido |
| El ogro es el único modelo no humanoide y Mixamo no vale | M4 | Se sustituye por un humanoide grande y deforme |
| El hub y el alijo se comen el tiempo | M3 | Se cae a: extraer = guardar partida, sin hub |

## 13. Decisiones abiertas

- ¿La antorcha se consume con el tiempo o es permanente?
- ¿El fijado de objetivo se rompe solo a cierta distancia?
- ¿Cuántas pociones se pueden guardar en el alijo?
