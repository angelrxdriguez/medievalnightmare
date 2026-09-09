# Convenciones

## Escala y greybox
- Personaje: 1.8 m de alto.
- Puertas: 2.2 m.
- Techos: 3 m.
- Rejilla de 1 m, snapping siempre activado.
- Todo el nivel se construye con CSG en gris (`assets/materials/greybox/`).
- No se toca el arte hasta que el juego sea jugable de principio a fin.

## Código
- C# es el lenguaje principal. GDScript solo para scripts triviales
  (puertas, triggers, carteles), que viven en `src/Props/`.
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
