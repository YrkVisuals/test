# DOOM // CONSOLE

Un FPS raycasting inspire de DOOM, realise entierement dans une application console .NET 8. Le projet ne contient aucun asset, sprite, son ou code de DOOM : les cartes, les monstres et les effets sont generes proceduralement.

## Lancer le jeu

Depuis ce dossier :

```powershell
dotnet run
```

Le jeu doit etre lance dans un vrai terminal interactif (Windows Terminal de preference). Une fenetre d'au moins 80 x 24 caracteres est recommandee. Si la sortie est redirigee, le programme affiche simplement les instructions pour ne pas rester bloque.

## Commandes

| Touche | Action |
|---|---|
| `Z/W` + `Q/A` + `S/D` | Se deplacer (AZERTY et QWERTY) |
| `Fleches gauche/droite` | Tourner la camera |
| `Fleches haut/bas` | Avancer / reculer |
| `Espace` | Tirer (maintenir pour le chaingun) |
| `E` | Ouvrir / fermer une porte |
| `1` a `5` | Changer d'arme |
| `M` | Afficher la carte du secteur |
| `P` | Pause |
| `Entree` | Commencer / continuer |
| `X` | Quitter |
| `Echap` | Pause ; retour de la carte ; quit depuis la pause |

`Shift` permet de courir.

## Contenu

- 3 secteurs avec portes, obstacles, sorties et objectif de nettoyage des hostiles.
- Moteur de raycasting 2.5D, profondeur par colonne, rendu ANSI 256 couleurs.
- Monstres avec IA de poursuite, navigation par champ de distance, attaques de contact et projectiles.
- 5 armes : pistolet, fusil a pompe, chaingun, lance-roquettes et rifle plasma.
- Projectiles, explosions de zone, particules, degats d'armure, score et sons systeme courts.
- Collecteurs de vie, armure, munitions, armes et cle de sortie.
- Ecran de titre, pause, carte, mort, victoire et transitions de niveaux.

Le prototype vise un rendu et des mecaniques de DOOM dans les limites d'un terminal : ce n'est pas une copie des assets ou du code du jeu original.
