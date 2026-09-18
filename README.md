# Dames

Jeu de **dames internationales** (damier 10×10) en C# / .NET 10 : un moteur de règles complet,
une IA alpha-bêta à quatre niveaux, une interface graphique Blazor WebAssembly et une interface console.

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

## Jouer dans le navigateur

```bash
dotnet run --project src/Dames.Web
```

Tout tourne côté client : le moteur et l'IA sont compilés en WebAssembly, il n'y a pas de serveur
de jeu. On clique une pièce pour voir ses coups ; une rafle s'annonce avant d'être jouée, avec son
chemin en laiton et une croix sur chaque pièce qu'elle emporte. Quand plusieurs chemins prennent le
même nombre de pièces, on choisit le sien en cliquant les cases l'une après l'autre.

Le damier se retourne tout seul pour que votre camp soit toujours en bas.

## Jouer dans le terminal

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
src/Dames.Web     interface graphique Blazor WebAssembly
src/Dames.Cli     interface console
tests/Dames.Core.Tests   52 tests (règles, perft, nulles, notation, IA)
```

`Dames.Core` ne dépend de rien d'autre que du framework : les deux interfaces le consomment tel
quel, et une troisième (bureau, service) n'aurait rien à réécrire.

Côté web, `GameSession` fait le pont : il tient la sélection en cours, le relevé et l'identité des
pièces affichées, de sorte qu'une pièce soit animée de case en case plutôt que redessinée. Le
navigateur n'ayant qu'un fil d'exécution, les temps de réflexion y sont raccourcis et une seule
boucle de jeu de l'ordinateur tourne à la fois.

### Déploiement

Le workflow `.github/workflows/pages.yml` publie `src/Dames.Web` sur GitHub Pages à chaque poussée
sur `main`. Il faut que Pages soit activé sur le dépôt avec « GitHub Actions » comme source ; sur un
compte gratuit, cela suppose un dépôt public.

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

- Import/export de parties au format PDN.
- Moteur en bitboards sur 50 cases, pour gagner un ordre de grandeur en vitesse de recherche.
- Bibliothèque d'ouvertures et tables de finales.
