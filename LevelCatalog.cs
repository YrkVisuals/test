namespace ConsoleApp1;

internal static class LevelCatalog
{
    public static IReadOnlyList<LevelBlueprint> Create()
    {
        return new[]
        {
            CreateOutpost(),
            CreateReactor(),
            CreateCitadel()
        };
    }

    private static LevelBlueprint CreateOutpost()
    {
        var map = new GridBuilder(36, 24);
        map.Room(2, 2, 10, 8);
        map.Room(16, 2, 8, 8);
        map.Room(27, 2, 7, 10);
        map.Room(3, 14, 10, 8);
        map.Room(17, 14, 15, 8);

        map.Corridor(4, 11, 29, 1);
        map.Corridor(6, 9, 1, 4);
        map.Corridor(20, 9, 1, 4);
        map.Corridor(30, 9, 1, 4);
        map.Corridor(8, 11, 1, 4);
        map.Corridor(25, 11, 1, 4);

        map.SolidBlock(7, 4, 3, 2, 'B');
        map.SolidBlock(18, 4, 3, 2, 'C');
        map.SolidBlock(31, 4, 2, 2, 'A');
        map.SolidBlock(6, 17, 3, 2, 'B');
        map.SolidBlock(23, 16, 4, 2, 'C');
        map.SolidBlock(14, 8, 2, 2, 'X');
        map.SolidBlock(27, 8, 2, 2, 'X');

        map.Door(20, 10);
        map.Door(30, 10);
        map.Door(8, 13);
        map.Door(25, 13);
        map.Exit(29, 19);

        return new LevelBlueprint(
            "OUTPOST",
            "THE RED FACILITY",
            map.ToLayout(),
            new GridPoint(4, 4),
            new[]
            {
                new EnemySpawn(EnemyKind.Imp, 10.5, 3.5),
                new EnemySpawn(EnemyKind.Imp, 22.5, 7.5),
                new EnemySpawn(EnemyKind.Spitter, 29.5, 5.5),
                new EnemySpawn(EnemyKind.Imp, 31.5, 9.5),
                new EnemySpawn(EnemyKind.Brute, 5.5, 16.5),
                new EnemySpawn(EnemyKind.Imp, 10.5, 20.5),
                new EnemySpawn(EnemyKind.Spitter, 19.5, 20.5),
                new EnemySpawn(EnemyKind.Imp, 28.5, 15.5),
                new EnemySpawn(EnemyKind.Brute, 27.5, 20.5)
            },
            new[]
            {
                new PickupSpawn(PickupKind.Health, 4.5, 8.5, 25),
                new PickupSpawn(PickupKind.Weapon, 30.5, 3.5, (int)WeaponType.Shotgun),
                new PickupSpawn(PickupKind.Ammo, 18.5, 8.5, 12, AmmoType.Shells),
                new PickupSpawn(PickupKind.Armor, 4.5, 20.5, 40),
                new PickupSpawn(PickupKind.Key, 20.5, 20.5),
                new PickupSpawn(PickupKind.Ammo, 28.5, 7.5, 8, AmmoType.Rockets),
                new PickupSpawn(PickupKind.Ammo, 10.5, 3.5, 25),
                new PickupSpawn(PickupKind.Weapon, 9.5, 19.5, (int)WeaponType.Chaingun)
            },
            RequiresKey: true,
            RequiresClear: true);
    }

    private static LevelBlueprint CreateReactor()
    {
        var map = new GridBuilder(40, 24);
        map.Room(2, 3, 9, 7);
        map.Room(14, 2, 12, 6);
        map.Room(29, 3, 9, 7);
        map.Room(13, 10, 14, 9);
        map.Room(3, 16, 10, 6);
        map.Room(27, 16, 11, 6);

        map.Corridor(10, 6, 5, 1);
        map.Corridor(25, 6, 5, 1);
        map.Corridor(6, 9, 1, 8);
        map.Corridor(33, 9, 1, 8);
        map.Corridor(12, 14, 16, 1);
        map.Corridor(8, 14, 1, 4);
        map.Corridor(32, 14, 1, 4);
        map.Corridor(19, 7, 1, 5);

        map.SolidBlock(17, 12, 6, 3, 'A');
        map.SolidBlock(17, 16, 6, 2, 'C');
        map.SolidBlock(5, 5, 3, 2, 'B');
        map.SolidBlock(32, 5, 3, 2, 'B');
        map.SolidBlock(6, 19, 3, 2, 'X');
        map.SolidBlock(31, 19, 4, 2, 'X');
        map.SolidBlock(14, 3, 2, 3, 'C');
        map.SolidBlock(25, 3, 2, 3, 'C');

        map.Door(12, 6);
        map.Door(28, 6);
        map.Door(6, 12);
        map.Door(33, 12);
        map.Door(8, 16);
        map.Door(32, 16);
        map.Exit(35, 20);

        return new LevelBlueprint(
            "REACTOR",
            "NO SAFE CONTAINMENT",
            map.ToLayout(),
            new GridPoint(4, 5),
            new[]
            {
                new EnemySpawn(EnemyKind.Imp, 8.5, 4.5),
                new EnemySpawn(EnemyKind.Imp, 16.5, 4.5),
                new EnemySpawn(EnemyKind.Spitter, 23.5, 4.5),
                new EnemySpawn(EnemyKind.Imp, 35.5, 5.5),
                new EnemySpawn(EnemyKind.Brute, 16.5, 11.5),
                new EnemySpawn(EnemyKind.Spitter, 23.5, 13.5),
                new EnemySpawn(EnemyKind.Imp, 19.5, 15.5),
                new EnemySpawn(EnemyKind.Brute, 10.5, 20.5),
                new EnemySpawn(EnemyKind.Imp, 28.5, 20.5),
                new EnemySpawn(EnemyKind.Spitter, 36.5, 20.5)
            },
            new[]
            {
                new PickupSpawn(PickupKind.Health, 3.5, 8.5, 30),
                new PickupSpawn(PickupKind.Ammo, 24.5, 3.5, 20, AmmoType.Rockets),
                new PickupSpawn(PickupKind.Weapon, 35.5, 3.5, (int)WeaponType.RocketLauncher),
                new PickupSpawn(PickupKind.Armor, 5.5, 18.5, 45),
                new PickupSpawn(PickupKind.Ammo, 19.5, 18.5, 10, AmmoType.Cells),
                new PickupSpawn(PickupKind.Health, 28.5, 18.5, 35),
                new PickupSpawn(PickupKind.Weapon, 19.5, 11.5, (int)WeaponType.PlasmaRifle),
                new PickupSpawn(PickupKind.Ammo, 33.5, 8.5, 8, AmmoType.Shells)
            },
            RequiresKey: false,
            RequiresClear: true);
    }

    private static LevelBlueprint CreateCitadel()
    {
        var map = new GridBuilder(44, 26);
        map.Room(2, 2, 9, 7);
        map.Room(15, 2, 8, 7);
        map.Room(27, 2, 8, 7);
        map.Room(37, 2, 5, 7);
        map.Room(3, 12, 8, 7);
        map.Room(15, 12, 8, 9);
        map.Room(27, 12, 8, 7);
        map.Room(37, 12, 5, 11);
        map.Room(3, 21, 16, 3);
        map.Room(23, 21, 19, 3);

        map.Corridor(10, 5, 6, 1);
        map.Corridor(22, 5, 6, 1);
        map.Corridor(34, 5, 4, 1);
        map.Corridor(6, 8, 1, 6);
        map.Corridor(19, 8, 1, 6);
        map.Corridor(31, 8, 1, 6);
        map.Corridor(39, 8, 1, 6);
        map.Corridor(10, 15, 6, 1);
        map.Corridor(22, 15, 6, 1);
        map.Corridor(34, 15, 4, 1);
        map.Corridor(19, 19, 1, 4);
        map.Corridor(31, 19, 1, 4);
        map.Corridor(8, 20, 26, 1);

        map.SolidBlock(5, 4, 3, 2, 'B');
        map.SolidBlock(17, 4, 3, 2, 'C');
        map.SolidBlock(29, 4, 3, 2, 'A');
        map.SolidBlock(39, 4, 2, 3, 'X');
        map.SolidBlock(5, 14, 3, 2, 'B');
        map.SolidBlock(17, 14, 3, 2, 'C');
        map.SolidBlock(29, 14, 3, 2, 'B');
        map.SolidBlock(39, 15, 2, 3, 'A');
        map.SolidBlock(10, 22, 3, 2, 'X');
        map.SolidBlock(29, 22, 4, 2, 'X');
        map.SolidBlock(21, 13, 2, 3, 'A');
        map.SolidBlock(35, 21, 2, 2, 'B');

        map.Door(12, 5);
        map.Door(24, 5);
        map.Door(36, 5);
        map.Door(6, 11);
        map.Door(19, 11);
        map.Door(31, 11);
        map.Door(39, 11);
        map.Door(12, 15);
        map.Door(24, 15);
        map.Door(36, 15);
        map.Door(19, 22);
        map.Door(31, 22);
        map.Exit(41, 20);

        return new LevelBlueprint(
            "CITADEL",
            "THE LAST SIGNAL",
            map.ToLayout(),
            new GridPoint(4, 5),
            new[]
            {
                new EnemySpawn(EnemyKind.Imp, 8.5, 4.5),
                new EnemySpawn(EnemyKind.Spitter, 16.5, 4.5),
                new EnemySpawn(EnemyKind.Brute, 28.5, 4.5),
                new EnemySpawn(EnemyKind.Imp, 38.5, 4.5),
                new EnemySpawn(EnemyKind.Spitter, 9.5, 15.5),
                new EnemySpawn(EnemyKind.Brute, 18.5, 17.5),
                new EnemySpawn(EnemyKind.Imp, 30.5, 17.5),
                new EnemySpawn(EnemyKind.Spitter, 38.5, 17.5),
                new EnemySpawn(EnemyKind.Imp, 6.5, 22.5),
                new EnemySpawn(EnemyKind.Spitter, 25.5, 22.5),
                new EnemySpawn(EnemyKind.Brute, 33.5, 22.5),
                new EnemySpawn(EnemyKind.Boss, 40.5, 20.5)
            },
            new[]
            {
                new PickupSpawn(PickupKind.Health, 3.5, 8.5, 40),
                new PickupSpawn(PickupKind.Armor, 8.5, 15.5, 50),
                new PickupSpawn(PickupKind.Ammo, 28.5, 4.5, 20, AmmoType.Rockets),
                new PickupSpawn(PickupKind.Health, 28.5, 17.5, 40),
                new PickupSpawn(PickupKind.Ammo, 38.5, 17.5, 12, AmmoType.Cells),
                new PickupSpawn(PickupKind.Weapon, 25.5, 22.5, (int)WeaponType.RocketLauncher),
                new PickupSpawn(PickupKind.Ammo, 23.5, 22.5, 8)
            },
            RequiresKey: false,
            RequiresClear: true);
    }

    private sealed class GridBuilder
    {
        public GridBuilder(int width, int height)
        {
            Width = width;
            Height = height;
            Tiles = new char[height, width];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Tiles[y, x] = '#';
                }
            }
        }

        private int Width { get; }

        private int Height { get; }

        private char[,] Tiles { get; }

        public void Room(int x, int y, int width, int height)
        {
            Fill(x, y, width, height, '.');
        }

        public void Corridor(int x, int y, int width, int height)
        {
            Fill(x, y, width, height, '.');
        }

        public void SolidBlock(int x, int y, int width, int height, char tile)
        {
            Fill(x, y, width, height, tile);
        }

        public void Door(int x, int y)
        {
            if (Inside(x, y))
            {
                Tiles[y, x] = 'D';
            }
        }

        public void Exit(int x, int y)
        {
            if (Inside(x, y))
            {
                Tiles[y, x] = 'E';
            }
        }

        public string[] ToLayout()
        {
            var rows = new string[Height];
            for (var y = 0; y < Height; y++)
            {
                var chars = new char[Width];
                for (var x = 0; x < Width; x++)
                {
                    chars[x] = Tiles[y, x];
                }

                rows[y] = new string(chars);
            }

            return rows;
        }

        private void Fill(int x, int y, int width, int height, char tile)
        {
            for (var py = y; py < y + height; py++)
            {
                for (var px = x; px < x + width; px++)
                {
                    if (Inside(px, py))
                    {
                        Tiles[py, px] = tile;
                    }
                }
            }
        }

        private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    }
}
