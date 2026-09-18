# Dames

Jeu de **dames internationales** (damier 10×10) en C# / .NET 10 : un moteur de règles complet,
une IA alpha-bêta à quatre niveaux, et une interface console.

```
     a   b   c   d   e   f   g   h   i   j
   +---+---+---+---+---+---+---+---+---+---+
10 |   | x |   | x |   | x |   | x |   | x | 10
   +---+---+---+---+---+---+---+---+---+---+
 9 | x |   | x |   | x |   | x |   | x |   | 9
   +---+---+---+---+---+---+---+---+---+---+
 …
 5 | 26|   | 27|   | 28|   | 29|   | 30|   | 5
   +---+---+---+---+---+---+---+---+---+---+
 4 |   | o |   | o |   | o |   | o |   | o | 4
   +---+---+---+---+---+---+---+---+---+---+
```

## Démarrer

```bash
dotnet run --project src/Dames.Cli
```

Sans argument, le programme demande le mode de jeu et le niveau. En ligne de commande :

```bash
dotnet run --project src/Dames.Cli -- --humain-vs-ia --niveau difficile
dotnet run --project src/Dames.Cli -- --ia-vs-ia --niveau expert --sans-numeros
```

| Option | Effet |
| --- | --- |
| `--humain-vs-ia` | Vous jouez les Blancs (défaut) |
| `--ia-vs-humain` | Vous jouez les Noirs |
| `--humain-vs-humain` | Deux joueurs sur le même clavier |
| `--ia-vs-ia` | Démonstration, l'ordinateur joue les deux camps |
| `--niveau <n>` | `facile`, `moyen`, `difficile`, `expert` |
| `--sans-numeros` | Masque les numéros des cases vides |

Pendant la partie, on saisit les coups en **notation officielle** : `32-28` pour un déplacement,
`32x23` pour une prise, `32x23x12` pour détailler une rafle. Les commandes `coups`, `annuler`
et `quitter` sont disponibles à tout moment.

## Règles implémentées

Les règles de la FMJD, telles qu'on les joue en France :

- damier 10×10, 20 pions par camp, cases sombres numérotées de 1 à 50 ;
- le pion avance d'une case en diagonale, mais **prend dans les quatre directions** ;
- la **dame est volante** : elle glisse sur toute une diagonale, et prend à distance en
  choisissant librement sa case d'arrivée derrière la pièce prise ;
- la **prise est obligatoire**, et l'on doit prendre le **maximum de pièces** ;
- une pièce déjà prise reste sur le damier jusqu'à la fin de la rafle : elle ne peut pas être
  reprise, mais elle bloque le passage (« coup turc ») ;
- un pion qui ne fait que **traverser** la rangée de promotion en cours de rafle n'est pas promu ;
- la partie est perdue par le camp qui n'a plus de pièce ou plus aucun coup légal ; elle est nulle
  par triple répétition ou après 50 demi-coups sans prise ni mouvement de pion.

La conformité est vérifiée par un test de **perft** depuis la position initiale, comparé aux valeurs
de référence publiées : 9, 81, 658, 4 265 et 27 117 parties distinctes de 1 à 5 demi-coups.

## L'IA

`SearchEngine` est un negamax avec élagage alpha-bêta :

- approfondissement itératif sous contrainte de temps ;
- table de transposition de 2²⁰ entrées indexée par hachage Zobrist ;
- tri des coups (coup de la table, rafles les plus longues, coups tueurs) ;
- extension des rafles : les prises étant obligatoires, une position en cours de rafle n'est
  jamais évaluée statiquement ;
- évaluation matérielle (pion 100, dame 320) corrigée par l'avancement des pions, la tenue de la
  rangée de fond, la sécurité des bords et la centralisation des dames.

| Niveau | Profondeur max | Temps par coup | Aléa |
| --- | --- | --- | --- |
| Facile | 2 | 0,2 s | ±1,2 pion |
| Moyen | 5 | 0,8 s | ±0,3 pion |
| Difficile | 12 | 2 s | aucun |
| Expert | 24 | 6 s | aucun |

En pratique, le niveau Expert atteint la profondeur 11 en 6 secondes depuis la position initiale.

## Organisation

```
src/Dames.Core    moteur : damier, génération des coups, état de partie, évaluation, recherche
src/Dames.Cli     interface console
tests/Dames.Core.Tests   52 tests (règles, perft, nulles, notation, IA)
```

`Dames.Core` ne dépend de rien d'autre que du framework : il est réutilisable tel quel derrière
une autre interface (web, desktop, service).

## Développer

```bash
dotnet build
dotnet test
```

Les positions de test s'écrivent de façon compacte, ce qui rend les cas de règles lisibles :

```csharp
// 32 prend deux pièces, 35 n'en prend qu'une : seule la rafle maximale est légale.
List<Move> moves = MoveGenerator.Generate(
    Board.FromPositionString("W:32,35 B:18,28,30"), Player.White);

Assert.Equal("32x23x12", Assert.Single(moves).ToNotation());
```

## Pistes

- Interface graphique (Blazor ou Avalonia) par-dessus `Dames.Core`.
- Import/export de parties au format PDN.
- Bibliothèque d'ouvertures et tables de finales.
