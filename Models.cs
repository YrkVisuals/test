using System.Collections.Generic;

namespace ConsoleApp1;

internal enum GameScreen
{
    Title,
    Playing,
    Paused,
    Map,
    Dead,
    Victory,
    Quit
}

internal enum WeaponType
{
    Pistol,
    Shotgun,
    Chaingun,
    RocketLauncher,
    PlasmaRifle
}

internal enum AmmoType
{
    Bullets,
    Shells,
    Rockets,
    Cells
}

internal enum PickupKind
{
    Health,
    Armor,
    Ammo,
    Weapon,
    Key,
    Exit
}

internal enum EnemyKind
{
    Imp,
    Brute,
    Spitter,
    Boss
}

internal enum ProjectileOwner
{
    Player,
    Enemy
}

internal readonly record struct GridPoint(int X, int Y);

internal readonly record struct EnemySpawn(EnemyKind Kind, double X, double Y);

internal readonly record struct PickupSpawn(PickupKind Kind, double X, double Y, int Amount = 0, AmmoType Ammo = AmmoType.Bullets);

internal sealed record LevelBlueprint(
    string Name,
    string Subtitle,
    string[] Layout,
    GridPoint PlayerStart,
    IReadOnlyList<EnemySpawn> Enemies,
    IReadOnlyList<PickupSpawn> Pickups,
    bool RequiresKey = false,
    bool RequiresClear = true);

internal sealed class MapData
{
    private readonly Dictionary<(int X, int Y), Door> _doors = new();

    public MapData(IReadOnlyList<string> layout)
    {
        var height = layout.Count;
        var width = layout.Count == 0 ? 0 : layout.Max(row => row.Length);
        Tiles = new char[height, width];

        for (var y = 0; y < height; y++)
        {
            var row = layout[y];
            for (var x = 0; x < width; x++)
            {
                var tile = x < row.Length ? row[x] : '#';
                Tiles[y, x] = tile;
                if (tile == 'D')
                {
                    _doors[(x, y)] = new Door(x, y);
                }
            }
        }

        Width = width;
        Height = height;
    }

    public char[,] Tiles { get; }

    public int Width { get; }

    public int Height { get; }

    public IEnumerable<Door> Doors => _doors.Values;

    public char TileAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return '#';
        }

        return Tiles[y, x];
    }

    public void SetTile(int x, int y, char tile)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height)
        {
            Tiles[y, x] = tile;
        }
    }

    public bool IsSolid(int x, int y)
    {
        var tile = TileAt(x, y);
        if (tile == 'D')
        {
            return !(_doors.TryGetValue((x, y), out var door) && door.Open);
        }

        return tile is '#' or 'X' or 'B' or 'C' or 'A';
    }

    public bool IsWallTile(char tile) => tile is '#' or 'X' or 'B' or 'C' or 'A' or 'D';

    public bool IsNavigable(int x, int y)
    {
        if (!IsInside(x, y))
        {
            return false;
        }

        return TileAt(x, y) is not ('#' or 'X' or 'B' or 'C' or 'A');
    }

    public Door? GetDoor(int x, int y) => _doors.TryGetValue((x, y), out var door) ? door : null;

    public bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}

internal sealed class Door
{
    public Door(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public bool Open { get; set; }
}

internal sealed record WeaponDefinition(
    WeaponType Type,
    string Name,
    string ShortName,
    AmmoType? Ammo,
    int Damage,
    int Pellets,
    double Spread,
    double FireDelay,
    double Range,
    bool Projectile,
    double ProjectileSpeed = 0,
    double SplashRadius = 0,
    int ProjectileColor = 208,
    string Description = "")
{
    public bool UsesAmmo => Ammo.HasValue;
}

internal static class WeaponCatalog
{
    public static readonly WeaponDefinition[] All =
    {
        new(WeaponType.Pistol, "PISTOL", "PIST", null, 34, 1, 0.006, 0.27, 32, false,
            Description: "Service sidearm — infinite reserve"),
        new(WeaponType.Shotgun, "SHOTGUN", "SG", AmmoType.Shells, 15, 8, 0.13, 0.72, 20, false,
            Description: "Eight pellets of close-range democracy"),
        new(WeaponType.Chaingun, "CHAINGUN", "GUN", AmmoType.Bullets, 16, 1, 0.065, 0.095, 32, false,
            Description: "A very persuasive argument"),
        new(WeaponType.RocketLauncher, "ROCKET LAUNCHER", "ROCK", AmmoType.Rockets, 70, 1, 0.01, 0.82, 35, true,
            ProjectileSpeed: 8.5, SplashRadius: 1.7, ProjectileColor: 202,
            Description: "Results may vary. Usually loudly"),
        new(WeaponType.PlasmaRifle, "PLASMA RIFLE", "PLSM", AmmoType.Cells, 26, 1, 0.025, 0.13, 35, true,
            ProjectileSpeed: 13, SplashRadius: 0.15, ProjectileColor: 81,
            Description: "Hot, blue, and deeply unpleasant")
    };

    public static WeaponDefinition Get(WeaponType type) => All[(int)type];
}

internal sealed class Player
{
    private readonly bool[] _unlockedWeapons = new bool[WeaponCatalog.All.Length];

    public Player()
    {
        _unlockedWeapons[(int)WeaponType.Pistol] = true;
        SelectedWeapon = (int)WeaponType.Pistol;
        Ammo[AmmoType.Bullets] = 55;
        Ammo[AmmoType.Shells] = 0;
        Ammo[AmmoType.Rockets] = 0;
        Ammo[AmmoType.Cells] = 0;
    }

    public double X;
    public double Y;
    public double Angle;

    public int Health { get; set; } = 100;

    public int Armor { get; set; }

    public int Score { get; set; }

    public int Kills { get; set; }

    public int SelectedWeapon { get; set; }

    public bool HasKey { get; set; }

    public double FireCooldown { get; set; }

    public double WeaponSway { get; set; }

    public double WalkTime { get; set; }

    public double DamageFlash { get; set; }

    public double MuzzleFlash { get; set; }

    public double ScreenShake { get; set; }

    public string Message { get; private set; } = string.Empty;

    public double MessageTime { get; private set; }

    public Dictionary<AmmoType, int> Ammo { get; } = new()
    {
        [AmmoType.Bullets] = 0,
        [AmmoType.Shells] = 0,
        [AmmoType.Rockets] = 0,
        [AmmoType.Cells] = 0
    };

    public WeaponType CurrentWeapon => (WeaponType)SelectedWeapon;

    public WeaponDefinition CurrentWeaponDefinition => WeaponCatalog.Get(CurrentWeapon);

    public bool IsUnlocked(WeaponType weapon) => _unlockedWeapons[(int)weapon];

    public void Unlock(WeaponType weapon) => _unlockedWeapons[(int)weapon] = true;

    public bool IsLocked(WeaponType weapon) => !_unlockedWeapons[(int)weapon];

    public void ResetForLevel(GridPoint start, double angle)
    {
        X = start.X + 0.5;
        Y = start.Y + 0.5;
        Angle = angle;
        Health = 100;
        Armor = 0;
        HasKey = false;
        FireCooldown = 0;
        WeaponSway = 0;
        WalkTime = 0;
        DamageFlash = 0;
        MuzzleFlash = 0;
        ScreenShake = 0;
        SelectedWeapon = (int)WeaponType.Pistol;
        // On conserve un peu de munitions pour ne pas rendre un changement
        // de secteur frustrant apres avoir debloque une arme.
        Ammo[AmmoType.Bullets] = Math.Max(70, Ammo[AmmoType.Bullets]);
        Ammo[AmmoType.Shells] = Math.Max(Ammo[AmmoType.Shells], IsUnlocked(WeaponType.Shotgun) ? 12 : 0);
        Ammo[AmmoType.Rockets] = Math.Max(Ammo[AmmoType.Rockets], IsUnlocked(WeaponType.RocketLauncher) ? 8 : 0);
        Ammo[AmmoType.Cells] = Math.Max(Ammo[AmmoType.Cells], IsUnlocked(WeaponType.PlasmaRifle) ? 20 : 0);
    }

    public void ResetCampaign()
    {
        Array.Clear(_unlockedWeapons, 0, _unlockedWeapons.Length);
        _unlockedWeapons[(int)WeaponType.Pistol] = true;
        Score = 0;
        Kills = 0;
        Health = 100;
        Armor = 0;
        HasKey = false;
        SelectedWeapon = (int)WeaponType.Pistol;
        Ammo[AmmoType.Bullets] = 0;
        Ammo[AmmoType.Shells] = 0;
        Ammo[AmmoType.Rockets] = 0;
        Ammo[AmmoType.Cells] = 0;
        FireCooldown = 0;
        WeaponSway = 0;
        WalkTime = 0;
        DamageFlash = 0;
        MuzzleFlash = 0;
        ScreenShake = 0;
        Message = string.Empty;
        MessageTime = 0;
    }

    public void SetMessage(string message, double duration = 2.5)
    {
        Message = message;
        MessageTime = duration;
    }

    public void TickEffects(double dt)
    {
        FireCooldown = Math.Max(0, FireCooldown - dt);
        WeaponSway = Math.Max(0, WeaponSway - dt * 5.5);
        MessageTime = Math.Max(0, MessageTime - dt);
        DamageFlash = Math.Max(0, DamageFlash - dt * 2.6);
        MuzzleFlash = Math.Max(0, MuzzleFlash - dt * 12);
        ScreenShake = Math.Max(0, ScreenShake - dt * 5);
    }

    public int TakeDamage(int amount)
    {
        var absorbed = Math.Min(Armor, Math.Max(0, amount / 2));
        Armor -= absorbed;
        var healthDamage = amount - absorbed;
        Health = Math.Max(0, Health - healthDamage);
        DamageFlash = 1;
        ScreenShake = Math.Min(1.2, ScreenShake + 0.7);
        return healthDamage;
    }

    public void AddHealth(int amount) => Health = Math.Min(100, Health + amount);

    public void AddArmor(int amount) => Armor = Math.Min(100, Armor + amount);

    public void AddAmmo(AmmoType type, int amount) => Ammo[type] = Math.Min(999, Ammo[type] + amount);

    public bool Consume(WeaponDefinition weapon)
    {
        if (!weapon.UsesAmmo)
        {
            return true;
        }

        var ammo = weapon.Ammo!.Value;
        if (Ammo[ammo] <= 0)
        {
            return false;
        }

        Ammo[ammo]--;
        return true;
    }
}

internal sealed class Enemy
{
    public Enemy(EnemyKind kind, double x, double y)
    {
        Kind = kind;
        X = x;
        Y = y;
        var data = EnemyCatalog.Get(kind);
        Health = data.MaxHealth;
    }

    public EnemyKind Kind { get; }

    public double X;
    public double Y;

    public int Health { get; set; }

    public bool Alive { get; set; } = true;

    public double AttackTimer { get; set; }

    public double AnimationTime { get; set; }

    public double HitFlash { get; set; }

    public double WanderAngle { get; set; }

    public int LastAttackDirection { get; set; }

    public EnemyDefinition Definition => EnemyCatalog.Get(Kind);
}

internal sealed record EnemyDefinition(
    string Name,
    int MaxHealth,
    double Speed,
    double Radius,
    int Damage,
    double AttackDelay,
    bool Ranged,
    int Score,
    int Color,
    int AccentColor,
    double Scale,
    string[] Pattern);

internal static class EnemyCatalog
{
    public static readonly EnemyDefinition[] All =
    {
        new("FAST RAIDER", 38, 2.15, 0.27, 8, 0.9, false, 100, 196, 226, 0.78,
            new[]
            {
                "  ^  ",
                " 0^0 ",
                "  v  ",
                " ||| ",
                " ||| ",
                " / \\ ",
                " \\/ "
            }),
        new("IRON BRUTE", 115, 1.15, 0.38, 19, 1.25, false, 250, 208, 220, 1.12,
            new[]
            {
                " ##### ",
                " #ooo# ",
                " #oOo# ",
                " ##### ",
                " ||||| ",
                " |###| ",
                " /   \\ "
            }),
        new("VOID SPITTER", 65, 1.25, 0.33, 14, 1.65, true, 200, 93, 129, 0.95,
            new[]
            {
                "  .-.  ",
                " (o.o) ",
                "  \\_/  ",
                "  | |  ",
                " /   \\ ",
                " |   | ",
                "  \\_/ "
            }),
        new("WARDEN PRIME", 420, 0.72, 0.58, 30, 1.05, true, 1500, 40, 49, 1.55,
            new[]
            {
                "  #####  ",
                " #OOOOO# ",
                " #O#O#O# ",
                " #OOOOO# ",
                "  #####  ",
                " |||||   ",
                " |###|   ",
                " /   \\   "
            })
    };

    public static EnemyDefinition Get(EnemyKind kind) => All[(int)kind];
}

internal sealed class Pickup
{
    public Pickup(PickupKind kind, double x, double y, int amount = 0, AmmoType ammo = AmmoType.Bullets)
    {
        Kind = kind;
        X = x;
        Y = y;
        Amount = amount;
        AmmoType = ammo;
    }

    public PickupKind Kind { get; }

    public double X { get; }

    public double Y { get; }

    public int Amount { get; }

    public AmmoType AmmoType { get; }

    public bool Active { get; set; } = true;

    public double BobPhase { get; }
}

internal sealed class Projectile
{
    public Projectile(
        double x,
        double y,
        double dx,
        double dy,
        double speed,
        int damage,
        int color,
        ProjectileOwner owner,
        double radius = 0.12,
        double splashRadius = 0,
        double life = 4)
    {
        X = x;
        Y = y;
        DX = dx;
        DY = dy;
        Speed = speed;
        Damage = damage;
        Color = color;
        Owner = owner;
        Radius = radius;
        SplashRadius = splashRadius;
        Life = life;
    }

    public double X { get; set; }

    public double Y { get; set; }

    public double DX { get; }

    public double DY { get; }

    public double Speed { get; }

    public int Damage { get; }

    public int Color { get; }

    public ProjectileOwner Owner { get; }

    public double Radius { get; }

    public double SplashRadius { get; }

    public double Life { get; set; }
}

internal sealed class Particle
{
    public Particle(double x, double y, double vx, double vy, double life, int color, char glyph = '*')
    {
        X = x;
        Y = y;
        VX = vx;
        VY = vy;
        Life = life;
        MaxLife = life;
        Color = color;
        Glyph = glyph;
    }

    public double X { get; set; }

    public double Y { get; set; }

    public double VX { get; set; }

    public double VY { get; set; }

    public double Life { get; set; }

    public double MaxLife { get; }

    public int Color { get; }

    public char Glyph { get; }
}

internal sealed class Level
{
    public Level(LevelBlueprint blueprint)
    {
        Name = blueprint.Name;
        Subtitle = blueprint.Subtitle;
        Map = new MapData(blueprint.Layout);
        RequiresKey = blueprint.RequiresKey;
        RequiresClear = blueprint.RequiresClear;
        PlayerStart = blueprint.PlayerStart;
        Enemies = blueprint.Enemies.Select(spawn => new Enemy(spawn.Kind, spawn.X, spawn.Y)).ToList();
        Pickups = blueprint.Pickups.Select(spawn => new Pickup(spawn.Kind, spawn.X, spawn.Y, spawn.Amount, spawn.Ammo)).ToList();
        FlowField = new int[Map.Width, Map.Height];
        ResetFlowField();
    }

    public string Name { get; }

    public string Subtitle { get; }

    public MapData Map { get; }

    public bool RequiresKey { get; }

    public bool RequiresClear { get; }

    public GridPoint PlayerStart { get; }

    public List<Enemy> Enemies { get; }

    public List<Pickup> Pickups { get; }

    public int[,] FlowField { get; private set; }

    public GridPoint Exit
    {
        get
        {
            for (var y = 0; y < Map.Height; y++)
            {
                for (var x = 0; x < Map.Width; x++)
                {
                    if (Map.TileAt(x, y) == 'E')
                    {
                        return new GridPoint(x, y);
                    }
                }
            }

            return new GridPoint(Map.Width - 2, Map.Height - 2);
        }
    }

    public void ResetFlowField()
    {
        var width = Map.Width;
        var height = Map.Height;
        FlowField = new int[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                FlowField[x, y] = int.MaxValue;
            }
        }

        var startX = (int)Math.Clamp(Math.Floor(FlowPlayerX), 0, width - 1);
        var startY = (int)Math.Clamp(Math.Floor(FlowPlayerY), 0, height - 1);
        var queue = new Queue<GridPoint>();
        FlowField[startX, startY] = 0;
        queue.Enqueue(new GridPoint(startX, startY));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var distance = FlowField[current.X, current.Y];
            TryFlowNeighbor(current.X + 1, current.Y, distance, queue);
            TryFlowNeighbor(current.X - 1, current.Y, distance, queue);
            TryFlowNeighbor(current.X, current.Y + 1, distance, queue);
            TryFlowNeighbor(current.X, current.Y - 1, distance, queue);
        }
    }

    public double FlowPlayerX { get; set; }

    public double FlowPlayerY { get; set; }

    private void TryFlowNeighbor(int x, int y, int distance, Queue<GridPoint> queue)
    {
        if (!Map.IsNavigable(x, y) || FlowField[x, y] <= distance + 1)
        {
            return;
        }

        FlowField[x, y] = distance + 1;
        queue.Enqueue(new GridPoint(x, y));
    }
}
