using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1;

internal sealed partial class DoomConsoleGame
{
    private const int TargetFramesPerSecond = 30;
    private const double PlayerRadius = 0.22;
    private const double FieldOfView = 0.78;
    private readonly Random _random = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<ConsoleKey, double> _keySeen = new();
    private readonly HashSet<ConsoleKey> _actionKeysDown = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<Particle> _particles = new();
    private readonly List<LevelBlueprint> _levels;
    private readonly Player _player = new();

    private Level? _level;
    private GameScreen _screen = GameScreen.Title;
    private int _levelIndex;
    private double _time;
    private double _flowTimer;
    private double _ambientTimer;
    private double _shiftUntil;
    private double _lastBeepAt = double.NegativeInfinity;
    private int _audioBusy;
    private bool _levelTransition;
    private bool _terminalMode;
    private bool _inputAvailable;
    private int _terminalWidth = 100;
    private int _terminalHeight = 32;

    private DoomConsoleGame()
    {
        _levels = LevelCatalog.Create().ToList();
    }

    public static void Run()
    {
        var game = new DoomConsoleGame();
        game.Start();
    }

    private void Start()
    {
        if (!PrepareTerminal())
        {
            Console.WriteLine("DOOM // CONSOLE");
            Console.WriteLine("Ce jeu a besoin d'une vraie fenetre de terminal interactive.");
            Console.WriteLine(" Lancez-le depuis Windows Terminal ou l Invite de commandes.");
            return;
        }

        LoadLevel(0, false);
        Console.CancelKeyPress += HandleCancelKeyPress;
        var lastFrame = _clock.Elapsed.TotalSeconds;

        try
        {
            while (_screen != GameScreen.Quit)
            {
                var now = _clock.Elapsed.TotalSeconds;
                var delta = Math.Clamp(now - lastFrame, 0, 0.08);
                lastFrame = now;
                _time += delta;

                PollInput();
                Update(delta);
                Render();

                var frameBudget = 1.0 / TargetFramesPerSecond;
                var sleep = frameBudget - (_clock.Elapsed.TotalSeconds - now);
                if (sleep > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(sleep));
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= HandleCancelKeyPress;
            RestoreTerminal();
        }
    }

    private void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        _screen = GameScreen.Quit;
    }

    private bool PrepareTerminal()
    {
        try
        {
            _inputAvailable = !Console.IsInputRedirected;
            if (! _inputAvailable || Console.IsOutputRedirected)
            {
                return false;
            }

            Console.OutputEncoding = Encoding.UTF8;
            EnableVirtualTerminalProcessing();
            Console.CursorVisible = false;
            Console.Write("\x1b[?1049h\x1b[2J\x1b[H");
            _terminalMode = true;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void RestoreTerminal()
    {
        if (!_terminalMode)
        {
            return;
        }

        try
        {
            Console.Write("\x1b[0m\x1b[?25h\x1b[?1049l");
            Console.CursorVisible = true;
        }
        catch (Exception)
        {
            // Le terminal peut deja avoir ete ferme par l'utilisateur.
        }
    }

    private void LoadLevel(int index, bool startPlaying)
    {
        _levelIndex = Math.Clamp(index, 0, _levels.Count - 1);
        _level = new Level(_levels[_levelIndex]);
        _player.ResetForLevel(_level.PlayerStart, 0);
        _projectiles.Clear();
        _particles.Clear();
        _flowTimer = 0;
        _ambientTimer = 0;
        _levelTransition = false;
        _level.FlowPlayerX = _player.X;
        _level.FlowPlayerY = _player.Y;
        _level.ResetFlowField();
        _screen = startPlaying ? GameScreen.Playing : GameScreen.Title;
        _player.SetMessage(startPlaying
            ? $"SECTEUR { _levelIndex + 1 }  //  { _level.Name }"
            : "PRESSIONNE ENTREE POUR COMMENCER", startPlaying ? 3 : 0);
    }

    private void Update(double delta)
    {
        if (_level is null)
        {
            return;
        }

        _player.TickEffects(delta);
        _level.FlowPlayerX = _player.X;
        _level.FlowPlayerY = _player.Y;

        switch (_screen)
        {
            case GameScreen.Playing:
                UpdatePlaying(delta);
                break;
            case GameScreen.Title:
                UpdateAmbient(delta);
                break;
            case GameScreen.Paused:
            case GameScreen.Map:
            case GameScreen.Dead:
            case GameScreen.Victory:
                UpdateParticles(delta);
                break;
        }

        if (_player.Health <= 0 && _screen == GameScreen.Playing)
        {
            _screen = GameScreen.Dead;
            _player.SetMessage("TU ES MORT", 999);
        }
    }

    private void UpdatePlaying(double delta)
    {
        if (_level is null)
        {
            return;
        }

        UpdatePlayerMovement(delta);

        if (IsActionPressed(ConsoleKey.Spacebar))
        {
            TryFire();
        }

        _flowTimer -= delta;
        if (_flowTimer <= 0)
        {
            _level.ResetFlowField();
            _flowTimer = 0.18;
        }

        UpdateDoors();
        UpdateEnemies(delta);
        if (_screen != GameScreen.Playing)
        {
            UpdateParticles(delta);
            return;
        }

        UpdateProjectiles(delta);
        if (_screen != GameScreen.Playing)
        {
            UpdateParticles(delta);
            return;
        }

        UpdateParticles(delta);
        if (_screen != GameScreen.Playing)
        {
            return;
        }

        UpdatePickups();
        if (_screen != GameScreen.Playing)
        {
            return;
        }

        CheckExit();

        if (_player.Health <= 0)
        {
            _screen = GameScreen.Dead;
            _player.SetMessage("TU ES MORT", 999);
        }
    }

    private void UpdateAmbient(double delta)
    {
        _ambientTimer -= delta;
        if (_ambientTimer <= 0)
        {
            _ambientTimer = 0.08;
            // Le titre est anime independamment du temps de jeu.
            _time += 0.01;
        }
    }

    private void UpdatePlayerMovement(double delta)
    {
        double forwardInput = 0;
        double strafeInput = 0;
        var lookInput = 0;

        if (IsHeld(ConsoleKey.W) || IsHeld(ConsoleKey.Z) || IsHeld(ConsoleKey.UpArrow))
        {
            forwardInput++;
        }

        if (IsHeld(ConsoleKey.S) || IsHeld(ConsoleKey.DownArrow))
        {
            forwardInput--;
        }

        if (IsHeld(ConsoleKey.D))
        {
            strafeInput++;
        }

        if (IsHeld(ConsoleKey.A) || IsHeld(ConsoleKey.Q))
        {
            strafeInput--;
        }

        if (IsHeld(ConsoleKey.LeftArrow))
        {
            lookInput--;
        }

        if (IsHeld(ConsoleKey.RightArrow))
        {
            lookInput++;
        }

        var sprint = _time < _shiftUntil || IsVirtualKeyDown(0x10) ? 1.65 : 1;
        var movedDistance = 0.0;
        if (forwardInput != 0 || strafeInput != 0)
        {
            var length = Math.Sqrt(forwardInput * forwardInput + strafeInput * strafeInput);
            forwardInput /= length;
            strafeInput /= length;

            var speed = 3.15 * sprint;
            var directionX = Math.Cos(_player.Angle);
            var directionY = Math.Sin(_player.Angle);
            var moveX = (directionX * forwardInput - directionY * strafeInput) * speed * delta;
            var moveY = (directionY * forwardInput + directionX * strafeInput) * speed * delta;
            var oldX = _player.X;
            var oldY = _player.Y;

            MoveCircle(ref _player.X, ref _player.Y, moveX, moveY, PlayerRadius);
            movedDistance = Math.Sqrt(Math.Pow(_player.X - oldX, 2) + Math.Pow(_player.Y - oldY, 2));
        }

        if (movedDistance > 0.0001)
        {
            _player.WalkTime += delta * (sprint > 1 ? 12 : 9);
            _player.WeaponSway = Math.Min(1, _player.WeaponSway + delta * 0.8);
        }

        if (lookInput != 0)
        {
            _player.Angle = NormalizeAngle(_player.Angle + lookInput * delta * 2.15);
        }
    }

    private void UpdateDoors()
    {
        if (_level is null)
        {
            return;
        }

        // Les ennemis rabattent leurs poursuivants; une porte fermee peut
        // aussi s'ouvrir lorsqu'un ennemi arrive devant elle.
        foreach (var enemy in _level.Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            foreach (var door in _level.Map.Doors)
            {
                if (door.Open)
                {
                    continue;
                }

                var distance = Distance(enemy.X, enemy.Y, door.X + 0.5, door.Y + 0.5);
                var openingDistance = 0.75 + enemy.Definition.Radius;
                if (distance < openingDistance)
                {
                    door.Open = true;
                    _flowTimer = 0;
                    SpawnParticles(door.X + 0.5, door.Y + 0.5, 5, 244, '░', 0.5);
                }
            }
        }
    }

    private void UpdateEnemies(double delta)
    {
        if (_level is null)
        {
            return;
        }

        foreach (var enemy in _level.Enemies)
        {
            if (_screen != GameScreen.Playing)
            {
                break;
            }

            if (!enemy.Alive)
            {
                continue;
            }

            var definition = enemy.Definition;
            enemy.AnimationTime += delta;
            enemy.AttackTimer -= delta;
            enemy.HitFlash = Math.Max(0, enemy.HitFlash - delta);

            var deltaX = _player.X - enemy.X;
            var deltaY = _player.Y - enemy.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var canSee = distance < 18 && HasLineOfSight(enemy.X, enemy.Y, _player.X, _player.Y);

            if (distance < 1.15 + definition.Radius && canSee)
            {
                if (enemy.AttackTimer <= 0)
                {
                    DamagePlayer(definition.Damage);
                    enemy.AttackTimer = definition.AttackDelay;
                }

                continue;
            }

            if (definition.Ranged && canSee && distance < 10 && enemy.AttackTimer <= 0)
            {
                var shotX = deltaX / Math.Max(0.001, distance);
                var shotY = deltaY / Math.Max(0.001, distance);
                _projectiles.Add(new Projectile(
                    enemy.X,
                    enemy.Y,
                    shotX,
                    shotY,
                    4.4,
                    definition.Damage,
                    enemy.Kind == EnemyKind.Boss ? 201 : 198,
                    ProjectileOwner.Enemy,
                    0.15,
                    0,
                    3.2));
                enemy.AttackTimer = definition.AttackDelay;
                SpawnParticles(enemy.X + shotX * 0.2, enemy.Y + shotY * 0.2, 3, 198, '*', 0.18);
            }

            double moveX;
            double moveY;
            if (canSee && distance < 7.5)
            {
                moveX = deltaX / Math.Max(0.001, distance);
                moveY = deltaY / Math.Max(0.001, distance);
                var strafe = Math.Sin(_time * (1.3 + (int)enemy.Kind) + enemy.X) * 0.24;
                moveX += -moveY * strafe;
                moveY += moveX * strafe;
            }
            else
            {
                (moveX, moveY) = GetFlowDirection(enemy);
            }

            var normalization = Math.Sqrt(moveX * moveX + moveY * moveY);
            if (normalization > 0.001)
            {
                moveX /= normalization;
                moveY /= normalization;
                var speed = definition.Speed;
                if (definition.Ranged && canSee && distance < 4.5)
                {
                    speed *= 0.45;
                    moveX *= -1;
                    moveY *= -1;
                }

                MoveCircle(ref enemy.X, ref enemy.Y, moveX * speed * delta, moveY * speed * delta, definition.Radius);
            }
        }

        if (_screen != GameScreen.Playing)
        {
            return;
        }

        // Petit人与人ement de groupe pour que les monstres restent lisibles.
        for (var i = 0; i < _level.Enemies.Count; i++)
        {
            var first = _level.Enemies[i];
            if (!first.Alive)
            {
                continue;
            }

            for (var j = i + 1; j < _level.Enemies.Count; j++)
            {
                var second = _level.Enemies[j];
                if (!second.Alive)
                {
                    continue;
                }

                var dx = second.X - first.X;
                var dy = second.Y - first.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var minimum = first.Definition.Radius + second.Definition.Radius + 0.05;
                if (distance > 0.0001 && distance < minimum)
                {
                    var push = (minimum - distance) * 0.5;
                    var px = dx / distance * push;
                    var py = dy / distance * push;
                    MoveCircle(ref first.X, ref first.Y, -px, -py, first.Definition.Radius);
                    MoveCircle(ref second.X, ref second.Y, px, py, second.Definition.Radius);
                }
            }
        }
    }

    private (double X, double Y) GetFlowDirection(Enemy enemy)
    {
        if (_level is null)
        {
            return (0, 0);
        }

        var cellX = Math.Clamp((int)Math.Floor(enemy.X), 0, _level.Map.Width - 1);
        var cellY = Math.Clamp((int)Math.Floor(enemy.Y), 0, _level.Map.Height - 1);
        var bestX = cellX;
        var bestY = cellY;
        var best = _level.FlowField[cellX, cellY];
        Span<(int X, int Y)> directions = stackalloc (int, int)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        foreach (var (offsetX, offsetY) in directions)
        {
            var nextX = cellX + offsetX;
            var nextY = cellY + offsetY;
            if (!_level.Map.IsNavigable(nextX, nextY))
            {
                continue;
            }

            if (_level.FlowField[nextX, nextY] < best)
            {
                best = _level.FlowField[nextX, nextY];
                bestX = nextX;
                bestY = nextY;
            }
        }

        if (best == int.MaxValue)
        {
            return (Math.Cos(enemy.WanderAngle), Math.Sin(enemy.WanderAngle));
        }

        var targetX = bestX + 0.5;
        var targetY = bestY + 0.5;
        var directionX = targetX - enemy.X;
        var directionY = targetY - enemy.Y;
        var distance = Math.Sqrt(directionX * directionX + directionY * directionY);
        if (distance < 0.001)
        {
            return (0, 0);
        }

        enemy.WanderAngle = Math.Atan2(directionY, directionX);
        return (directionX / distance, directionY / distance);
    }

    private void UpdateProjectiles(double delta)
    {
        if (_level is null)
        {
            return;
        }

        for (var index = _projectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _projectiles[index];
            var oldX = projectile.X;
            var oldY = projectile.Y;
            var travelX = projectile.DX * projectile.Speed * delta;
            var travelY = projectile.DY * projectile.Speed * delta;
            var travelDistance = Math.Sqrt(travelX * travelX + travelY * travelY);
            projectile.X += travelX;
            projectile.Y += travelY;
            projectile.Life -= delta;

            if (travelDistance > 0.0001)
            {
                var directionX = travelX / travelDistance;
                var directionY = travelY / travelDistance;
                var wallHit = CastRay(oldX, oldY, directionX, directionY, travelDistance);

                // Collision balayee : le projectile ne peut plus traverser
                // un mur entre deux positions consecutives.
                if (wallHit.RayDistance < travelDistance - 0.001)
                {
                    projectile.X = wallHit.X;
                    projectile.Y = wallHit.Y;
                    ImpactProjectile(projectile);
                    _projectiles.RemoveAt(index);
                    continue;
                }

                if (projectile.Owner == ProjectileOwner.Player)
                {
                    Enemy? hitEnemy = null;
                    var hitFraction = double.MaxValue;
                    foreach (var enemy in _level.Enemies)
                    {
                        if (!enemy.Alive)
                        {
                            continue;
                        }

                        var fraction = SegmentCircleIntersection(
                            oldX,
                            oldY,
                            projectile.X,
                            projectile.Y,
                            enemy.X,
                            enemy.Y,
                            projectile.Radius + enemy.Definition.Radius);
                        if (fraction >= 0 && fraction <= 1 && fraction < hitFraction
                            && fraction * travelDistance <= wallHit.RayDistance + 0.01)
                        {
                            hitEnemy = enemy;
                            hitFraction = fraction;
                        }
                    }

                    if (hitEnemy is not null)
                    {
                        projectile.X = oldX + travelX * hitFraction;
                        projectile.Y = oldY + travelY * hitFraction;
                        if (projectile.SplashRadius > 0.2)
                        {
                            Explode(projectile, projectile.X, projectile.Y);
                        }
                        else
                        {
                            DamageEnemy(hitEnemy, projectile.Damage, projectile.Color);
                            SpawnParticles(projectile.X, projectile.Y, 7, projectile.Color, '*', 0.32);
                        }

                        _projectiles.RemoveAt(index);
                        continue;
                    }
                }
                else
                {
                    var hitFraction = SegmentCircleIntersection(
                        oldX,
                        oldY,
                        projectile.X,
                        projectile.Y,
                        _player.X,
                        _player.Y,
                        projectile.Radius + PlayerRadius);
                    if (hitFraction >= 0 && hitFraction <= 1
                        && hitFraction * travelDistance <= wallHit.RayDistance + 0.01)
                    {
                        projectile.X = oldX + travelX * hitFraction;
                        projectile.Y = oldY + travelY * hitFraction;
                        if (projectile.SplashRadius > 0.2)
                        {
                            Explode(projectile, projectile.X, projectile.Y);
                        }
                        else
                        {
                            DamagePlayer(projectile.Damage);
                            SpawnParticles(projectile.X, projectile.Y, 8, projectile.Color, '*', 0.35);
                        }

                        _projectiles.RemoveAt(index);
                        continue;
                    }
                }
            }

            if (projectile.Life <= 0 || _level.Map.IsSolid((int)Math.Floor(projectile.X), (int)Math.Floor(projectile.Y)))
            {
                ImpactProjectile(projectile);
                _projectiles.RemoveAt(index);
            }
        }
    }

    private static double SegmentCircleIntersection(
        double startX,
        double startY,
        double endX,
        double endY,
        double centerX,
        double centerY,
        double radius)
    {
        var segmentX = endX - startX;
        var segmentY = endY - startY;
        var lengthSquared = segmentX * segmentX + segmentY * segmentY;
        if (lengthSquared < 0.0000001)
        {
            return Distance(startX, startY, centerX, centerY) <= radius ? 0 : -1;
        }

        var projection = ((centerX - startX) * segmentX + (centerY - startY) * segmentY) / lengthSquared;
        projection = Math.Clamp(projection, 0, 1);
        var closestX = startX + segmentX * projection;
        var closestY = startY + segmentY * projection;
        return Distance(closestX, closestY, centerX, centerY) <= radius ? projection : -1;
    }

    private void ImpactProjectile(Projectile projectile)
    {
        if (projectile.Owner == ProjectileOwner.Player && projectile.SplashRadius > 0.2)
        {
            Explode(projectile, projectile.X, projectile.Y);
        }
        else
        {
            SpawnParticles(projectile.X, projectile.Y, 6, projectile.Color, projectile.Owner == ProjectileOwner.Enemy ? 'o' : '*', 0.28);
        }
    }

    private void Explode(Projectile projectile, double x, double y)
    {
        SpawnParticles(x, y, 30, projectile.Color, '*', 0.8);
        SpawnParticles(x, y, 14, 231, '+', 0.55);
        Beep(70, 90);

        if (projectile.Owner == ProjectileOwner.Player && _level is not null)
        {
            foreach (var enemy in _level.Enemies)
            {
                if (!enemy.Alive)
                {
                    continue;
                }

                var distance = Distance(x, y, enemy.X, enemy.Y);
                if (distance <= projectile.SplashRadius + enemy.Definition.Radius
                    && HasLineOfSight(x, y, enemy.X, enemy.Y))
                {
                    var damage = (int)Math.Round(projectile.Damage * Math.Clamp(1 - distance / (projectile.SplashRadius + 0.3), 0.25, 1));
                    DamageEnemy(enemy, damage, projectile.Color);
                }
            }
        }
        else if (projectile.Owner == ProjectileOwner.Enemy)
        {
            var distance = Distance(x, y, _player.X, _player.Y);
            if (distance <= projectile.SplashRadius + PlayerRadius
                && HasLineOfSight(x, y, _player.X, _player.Y))
            {
                DamagePlayer((int)Math.Round(projectile.Damage * Math.Clamp(1 - distance / (projectile.SplashRadius + 0.3), 0.25, 1)));
            }
        }
    }

    private void UpdateParticles(double delta)
    {
        for (var index = _particles.Count - 1; index >= 0; index--)
        {
            var particle = _particles[index];
            particle.X += particle.VX * delta;
            particle.Y += particle.VY * delta;
            particle.VX *= Math.Pow(0.06, delta);
            particle.VY *= Math.Pow(0.06, delta);
            particle.Life -= delta;
            if (particle.Life <= 0)
            {
                _particles.RemoveAt(index);
            }
        }

        while (_particles.Count > 700)
        {
            _particles.RemoveAt(0);
        }
    }

    private void UpdatePickups()
    {
        if (_level is null || _screen != GameScreen.Playing)
        {
            return;
        }

        foreach (var pickup in _level.Pickups)
        {
            if (!pickup.Active || Distance(pickup.X, pickup.Y, _player.X, _player.Y) > 0.58)
            {
                continue;
            }

            var collected = false;
            switch (pickup.Kind)
            {
                case PickupKind.Health:
                    if (_player.Health < 100)
                    {
                        _player.AddHealth(pickup.Amount);
                        _player.SetMessage($"+{pickup.Amount} SANTE");
                        collected = true;
                    }

                    break;
                case PickupKind.Armor:
                    if (_player.Armor < 100)
                    {
                        _player.AddArmor(pickup.Amount);
                        _player.SetMessage($"+{pickup.Amount} ARMURE");
                        collected = true;
                    }

                    break;
                case PickupKind.Ammo:
                    _player.AddAmmo(pickup.AmmoType, pickup.Amount);
                    _player.SetMessage($"+{pickup.Amount} MUNITIONS {AmmoLabel(pickup.AmmoType)}");
                    collected = true;
                    break;
                case PickupKind.Weapon:
                    var weapon = (WeaponType)Math.Clamp(pickup.Amount, 0, WeaponCatalog.All.Length - 1);
                    var wasLocked = _player.IsLocked(weapon);
                    _player.Unlock(weapon);
                    _player.SelectedWeapon = (int)weapon;
                    _player.SetMessage(wasLocked
                        ? $"ARMES ACQUISE // {WeaponCatalog.Get(weapon).Name}"
                        : $"{WeaponCatalog.Get(weapon).Name} SELECTIONNEE");
                    collected = true;
                    break;
                case PickupKind.Key:
                    _player.HasKey = true;
                    _player.SetMessage("CLE DE SORTIE ACQUISE");
                    collected = true;
                    break;
            }

            if (collected)
            {
                pickup.Active = false;
                _level.Map.SetTile((int)Math.Floor(pickup.X), (int)Math.Floor(pickup.Y), '.');
                Beep(880, 45);
            }
        }
    }

    private void CheckExit()
    {
        if (_level is null || _screen != GameScreen.Playing)
        {
            return;
        }

        var exit = _level.Exit;
        if (Distance(_player.X, _player.Y, exit.X + 0.5, exit.Y + 0.5) > 0.7)
        {
            return;
        }

        if (_level.RequiresClear)
        {
            var remainingEnemies = _level.Enemies.Count(enemy => enemy.Alive);
            if (remainingEnemies > 0)
            {
                if (_player.MessageTime <= 0.05)
                {
                    _player.SetMessage($"SECTEUR VERROUILLE // {remainingEnemies} HOSTILE(S)", 1.2);
                }

                return;
            }
        }

        if (_level.RequiresKey && !_player.HasKey)
        {
            if (_player.MessageTime <= 0.05)
            {
                _player.SetMessage("SORTIE VERROUILLEE // TROUVE LA CLE", 1.2);
            }

            return;
        }

        _player.Score += 500 + _levelIndex * 250;
        Beep(1046, 160);
        if (_levelIndex >= _levels.Count - 1)
        {
            _levelTransition = false;
            _screen = GameScreen.Victory;
            _player.SetMessage("LE SIGNAL EST LIBRE", 999);
        }
        else
        {
            _levelTransition = true;
            _player.SetMessage($"SECTEUR { _levelIndex + 1 } NETTOYAGE // ENTREE POUR CONTINUER", 999);
            _screen = GameScreen.Paused;
        }
    }

    private void TryFire()
    {
        if (_screen != GameScreen.Playing || _player.FireCooldown > 0)
        {
            return;
        }

        var weapon = _player.CurrentWeaponDefinition;
        if (!_player.Consume(weapon))
        {
            _player.FireCooldown = 0.24;
            _player.SetMessage($"PAS DE MUNITIONS // {weapon.ShortName}", 0.8);
            Beep(110, 45);
            return;
        }

        _player.FireCooldown = weapon.FireDelay;
        _player.MuzzleFlash = 0.08;
        _player.WeaponSway = 1;
        _player.ScreenShake = Math.Min(1, _player.ScreenShake + (weapon.Type == WeaponType.RocketLauncher ? 0.75 : 0.12));
        Beep(weapon.Type == WeaponType.Pistol ? 180 : 105, weapon.Type == WeaponType.Pistol ? 35 : 55);

        if (weapon.Projectile)
        {
            var dx = Math.Cos(_player.Angle);
            var dy = Math.Sin(_player.Angle);
            _projectiles.Add(new Projectile(
                _player.X + dx * 0.22,
                _player.Y + dy * 0.22,
                dx,
                dy,
                weapon.ProjectileSpeed,
                weapon.Damage,
                weapon.ProjectileColor,
                ProjectileOwner.Player,
                weapon.Type == WeaponType.RocketLauncher ? 0.2 : 0.12,
                weapon.SplashRadius,
                3.5));
            SpawnParticles(_player.X + dx * 0.25, _player.Y + dy * 0.25, 4, weapon.ProjectileColor, '*', 0.15);
            return;
        }

        for (var pellet = 0; pellet < weapon.Pellets; pellet++)
        {
            var spread = weapon.Pellets == 1
                ? (_random.NextDouble() - 0.5) * weapon.Spread
                : (_random.NextDouble() - 0.5) * weapon.Spread * 2;
            Hitscan(_player.Angle + spread, weapon.Damage, weapon.Range, weapon.ProjectileColor);
        }
    }

    private void Hitscan(double angle, int damage, double range, int color)
    {
        if (_level is null)
        {
            return;
        }

        var dx = Math.Cos(angle);
        var dy = Math.Sin(angle);
        var wallHit = CastRay(_player.X, _player.Y, dx, dy, range);
        var hitDistance = wallHit.RayDistance;
        Enemy? hitEnemy = null;

        foreach (var enemy in _level.Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            var relativeX = enemy.X - _player.X;
            var relativeY = enemy.Y - _player.Y;
            var along = relativeX * dx + relativeY * dy;
            var perpendicular = Math.Abs(relativeX * -dy + relativeY * dx);
            var radius = enemy.Definition.Radius + 0.08;
            if (along > 0.08 && along < hitDistance && perpendicular <= radius)
            {
                hitDistance = along;
                hitEnemy = enemy;
            }
        }

        if (hitEnemy is not null)
        {
            DamageEnemy(hitEnemy, damage, color);
            SpawnParticles(hitEnemy.X, hitEnemy.Y, 5, color, '*', 0.22);
        }
        else
        {
            SpawnParticles(wallHit.X, wallHit.Y, 3, 244, '.', 0.18);
        }
    }

    private void DamageEnemy(Enemy enemy, int damage, int color)
    {
        if (!enemy.Alive)
        {
            return;
        }

        enemy.Health -= damage;
        enemy.HitFlash = 0.12;
        SpawnParticles(enemy.X, enemy.Y, 4, color, '*', 0.25);
        if (enemy.Health > 0)
        {
            return;
        }

        enemy.Alive = false;
        _player.Kills++;
        _player.Score += enemy.Definition.Score;
        _player.SetMessage($"{enemy.Definition.Name} DETRUIT  +{enemy.Definition.Score}", 1.25);
        SpawnParticles(enemy.X, enemy.Y, 30, 160, '*', 0.9);
        SpawnParticles(enemy.X, enemy.Y, 12, 231, '+', 0.65);
        Beep(55, 100);

        if (enemy.Kind == EnemyKind.Boss)
        {
            _player.Score += 1000;
            _player.SetMessage("WARDEN PRIME // SIGNAL DETRUIT", 2.2);
        }
        else if (_random.NextDouble() < 0.24)
        {
            var ammo = _player.CurrentWeaponDefinition.Ammo ?? AmmoType.Bullets;
            var drop = new Pickup(PickupKind.Ammo, enemy.X, enemy.Y, _random.Next(4, 10), ammo);
            _level!.Pickups.Add(drop);
        }
    }

    private void DamagePlayer(int amount)
    {
        if (_screen != GameScreen.Playing || amount <= 0)
        {
            return;
        }

        var healthDamage = _player.TakeDamage(amount);
        if (healthDamage > 0)
        {
            _player.SetMessage($"IMPACT // -{healthDamage} SANTE", 0.65);
        }
        else
        {
            _player.SetMessage("ARMURE ABSORBE L'IMPACT", 0.55);
        }

        Beep(90, 65);
        if (_player.Health <= 0)
        {
            _screen = GameScreen.Dead;
            _player.SetMessage("TU ES MORT", 999);
        }
    }

    private void Interact()
    {
        if (_screen != GameScreen.Playing || _level is null)
        {
            return;
        }

        Door? closest = null;
        var closestDistance = 1.5;
        foreach (var door in _level.Map.Doors)
        {
            var distance = Distance(_player.X, _player.Y, door.X + 0.5, door.Y + 0.5);
            if (distance < closestDistance && CanInteractWithDoor(door))
            {
                closest = door;
                closestDistance = distance;
            }
        }

        if (closest is null)
        {
            var exit = _level.Exit;
            if (Distance(_player.X, _player.Y, exit.X + 0.5, exit.Y + 0.5) < 1.1)
            {
                var remainingEnemies = _level.Enemies.Count(enemy => enemy.Alive);
                _player.SetMessage(
                    _level.RequiresClear && remainingEnemies > 0
                        ? $"SECTEUR VERROUILLE // {remainingEnemies} HOSTILE(S)"
                        : _level.RequiresKey && !_player.HasKey
                            ? "TU N'AS PAS LA CLE"
                            : "LA SORTIE EST OUVERTE",
                    1.2);
            }

            return;
        }

        closest.Open = !closest.Open;
        _player.SetMessage(closest.Open ? "PORTE OUVERTE" : "PORTE FERMEE", 0.9);
        Beep(closest.Open ? 620 : 260, 60);
        SpawnParticles(closest.X + 0.5, closest.Y + 0.5, 5, 244, '|', 0.35);
    }

    private bool CanInteractWithDoor(Door door)
    {
        if (_level is null)
        {
            return false;
        }

        // Une porte fermee est solide : on vise donc une cellule voisine
        // walkable, jamais son centre qui estarrete le raycast.
        var targetX = door.X + 0.5;
        var targetY = door.Y + 0.5;
        Span<(int X, int Y)> neighbors = stackalloc (int, int)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        foreach (var (offsetX, offsetY) in neighbors)
        {
            var neighborX = door.X + offsetX;
            var neighborY = door.Y + offsetY;
            if (!_level.Map.IsInside(neighborX, neighborY) || _level.Map.IsSolid(neighborX, neighborY))
            {
                continue;
            }

            var neighborCenterX = neighborX + 0.5;
            var neighborCenterY = neighborY + 0.5;
            if (Distance(_player.X, _player.Y, neighborCenterX, neighborCenterY) <= 1.35
                && HasLineOfSight(_player.X, _player.Y, neighborCenterX, neighborCenterY))
            {
                return true;
            }
        }

        // Some maps use a diagonal approach; keep a small, generous fallback
        // for a player standing in the same open room.
        return Distance(_player.X, _player.Y, targetX, targetY) <= 0.9
            && CanOccupy(targetX, targetY, PlayerRadius * 0.8);
    }

    private void RestartLevel()
    {
        if (_level is null)
        {
            return;
        }

        var oldScore = _player.Score;
        LoadLevel(_levelIndex, true);
        _player.Score = oldScore;
    }

    private void StartNewGame()
    {
        _player.ResetCampaign();
        LoadLevel(0, true);
    }

    private void NextLevel()
    {
        if (_levelIndex < _levels.Count - 1)
        {
            LoadLevel(_levelIndex + 1, true);
        }
    }

    private void PollInput()
    {
        if (!_inputAvailable)
        {
            return;
        }

        RefreshActionKeys();
        try
        {
            var now = _time;
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                _keySeen[key.Key] = now;
                if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
                {
                    _shiftUntil = now + 0.22;
                }

                if (IsOneShotAction(key.Key) && _actionKeysDown.Add(key.Key))
                {
                    HandleKey(key.Key);
                }
            }
        }
        catch (InvalidOperationException)
        {
            _inputAvailable = false;
        }
        catch (IOException)
        {
            _inputAvailable = false;
        }
    }

    private void RefreshActionKeys()
    {
        var released = _actionKeysDown
            .Where(key => !IsPhysicalKeyDown(key)
                && (!_keySeen.TryGetValue(key, out var lastSeen) || _time - lastSeen > 0.25))
            .ToArray();
        foreach (var key in released)
        {
            _actionKeysDown.Remove(key);
        }
    }

    private static bool IsOneShotAction(ConsoleKey key) => key
        is ConsoleKey.Enter
        or ConsoleKey.Escape
        or ConsoleKey.P
        or ConsoleKey.M
        or ConsoleKey.E
        or ConsoleKey.X
        or ConsoleKey.R
        or ConsoleKey.D1
        or ConsoleKey.D2
        or ConsoleKey.D3
        or ConsoleKey.D4
        or ConsoleKey.D5;

    private void HandleKey(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.Enter:
                if (_screen == GameScreen.Title)
                {
                    _screen = GameScreen.Playing;
                    _player.SetMessage("SECTEUR 01 // RESTE CONCENTRE", 2.5);
                }
                else if (_screen == GameScreen.Dead)
                {
                    RestartLevel();
                }
                else if (_screen == GameScreen.Victory)
                {
                    StartNewGame();
                }
                else if (_screen == GameScreen.Paused && _levelTransition)
                {
                    NextLevel();
                }

                break;
            case ConsoleKey.Escape:
                if (_screen == GameScreen.Playing)
                {
                    _screen = GameScreen.Paused;
                }
                else if (_screen == GameScreen.Map)
                {
                    _screen = GameScreen.Playing;
                }
                else
                {
                    // Depuis le titre, la pause, la mort ou la victoire,
                    // la touche Echap ferme proprement la partie.
                    _screen = GameScreen.Quit;
                }

                break;
            case ConsoleKey.P:
                if (_screen == GameScreen.Playing)
                {
                    _screen = GameScreen.Paused;
                }
                else if (_screen == GameScreen.Paused && _levelTransition)
                {
                    NextLevel();
                }
                else if (_screen == GameScreen.Paused || _screen == GameScreen.Map)
                {
                    _screen = GameScreen.Playing;
                }

                break;
            case ConsoleKey.M:
                if (_screen == GameScreen.Playing)
                {
                    _screen = GameScreen.Map;
                }
                else if (_screen == GameScreen.Map)
                {
                    _screen = GameScreen.Playing;
                }

                break;
            case ConsoleKey.E:
                Interact();
                break;
            case ConsoleKey.X:
                _screen = GameScreen.Quit;
                break;
            case ConsoleKey.R:
                if (_screen == GameScreen.Dead || _screen == GameScreen.Victory)
                {
                    RestartLevel();
                }

                break;
            case ConsoleKey.D1:
            case ConsoleKey.D2:
            case ConsoleKey.D3:
            case ConsoleKey.D4:
            case ConsoleKey.D5:
                SelectWeapon((int)key - (int)ConsoleKey.D1);
                break;
        }
    }

    private void SelectWeapon(int index)
    {
        if (_screen != GameScreen.Playing || index < 0 || index >= WeaponCatalog.All.Length)
        {
            return;
        }

        var weapon = (WeaponType)index;
        if (_player.IsLocked(weapon))
        {
            _player.SetMessage("ARME VERROUILLEE // CHERCHE UN COLLECTEUR", 1.1);
            return;
        }

        _player.SelectedWeapon = index;
        _player.WeaponSway = 0.55;
    }

    private bool IsHeld(ConsoleKey key)
    {
        if (IsPhysicalKeyDown(key))
        {
            return true;
        }

        return _keySeen.TryGetValue(key, out var lastSeen) && _time - lastSeen < 0.17;
    }

    private static bool IsPhysicalKeyDown(ConsoleKey key)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return key switch
        {
            ConsoleKey.W or ConsoleKey.Z => IsVirtualKeyDown(0x57) || IsVirtualKeyDown(0x5A),
            ConsoleKey.A or ConsoleKey.Q => IsVirtualKeyDown(0x41) || IsVirtualKeyDown(0x51),
            ConsoleKey.S => IsVirtualKeyDown(0x53),
            ConsoleKey.D => IsVirtualKeyDown(0x44),
            ConsoleKey.UpArrow => IsVirtualKeyDown(0x26),
            ConsoleKey.DownArrow => IsVirtualKeyDown(0x28),
            ConsoleKey.LeftArrow => IsVirtualKeyDown(0x25),
            ConsoleKey.RightArrow => IsVirtualKeyDown(0x27),
            ConsoleKey.Spacebar => IsVirtualKeyDown(0x20),
            ConsoleKey.Enter => IsVirtualKeyDown(0x0D),
            ConsoleKey.Escape => IsVirtualKeyDown(0x1B),
            ConsoleKey.P => IsVirtualKeyDown(0x50),
            ConsoleKey.M => IsVirtualKeyDown(0x4D),
            ConsoleKey.E => IsVirtualKeyDown(0x45),
            ConsoleKey.X => IsVirtualKeyDown(0x58),
            ConsoleKey.R => IsVirtualKeyDown(0x52),
            ConsoleKey.D1 => IsVirtualKeyDown(0x31),
            ConsoleKey.D2 => IsVirtualKeyDown(0x32),
            ConsoleKey.D3 => IsVirtualKeyDown(0x33),
            ConsoleKey.D4 => IsVirtualKeyDown(0x34),
            ConsoleKey.D5 => IsVirtualKeyDown(0x35),
            _ => false
        };
    }

    private static bool IsVirtualKeyDown(int virtualKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private bool IsActionPressed(ConsoleKey key) => IsHeld(key);

    private void MoveCircle(ref double x, ref double y, double deltaX, double deltaY, double radius)
    {
        var nextX = x + deltaX;
        if (CanOccupy(nextX, y, radius))
        {
            x = nextX;
        }

        var nextY = y + deltaY;
        if (CanOccupy(x, nextY, radius))
        {
            y = nextY;
        }
    }

    private bool CanOccupy(double x, double y, double radius)
    {
        if (_level is null)
        {
            return false;
        }

        if (_level.Map.IsSolid((int)Math.Floor(x), (int)Math.Floor(y)))
        {
            return false;
        }

        for (var index = 0; index < 8; index++)
        {
            var angle = index * Math.PI / 4;
            var sampleX = x + Math.Cos(angle) * radius;
            var sampleY = y + Math.Sin(angle) * radius;
            if (_level.Map.IsSolid((int)Math.Floor(sampleX), (int)Math.Floor(sampleY)))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasLineOfSight(double fromX, double fromY, double toX, double toY)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (distance < 0.001)
        {
            return true;
        }

        var rayX = deltaX / distance;
        var rayY = deltaY / distance;
        var startX = fromX;
        var startY = fromY;
        if (_level is not null && _level.Map.IsSolid((int)Math.Floor(startX), (int)Math.Floor(startY)))
        {
            // Une explosion peut impacter exactement sur la face d'un mur;
            // on decale le depart dans la cellule ouverte la plus proche.
            startX += rayX * 0.03;
            startY += rayY * 0.03;
        }

        var hit = CastRay(startX, startY, rayX, rayY, distance + 0.05);
        return hit.RayDistance >= distance - 0.12;
    }

    private RayHit CastRay(double originX, double originY, double rayX, double rayY, double maxDistance)
    {
        if (_level is null)
        {
            return new RayHit(maxDistance, maxDistance, originX + rayX * maxDistance, originY + rayY * maxDistance, '#', 0);
        }

        var rayLength = Math.Sqrt(rayX * rayX + rayY * rayY);
        if (rayLength < 0.0000001)
        {
            return new RayHit(maxDistance, maxDistance, originX, originY, '#', 0);
        }

        rayX /= rayLength;
        rayY /= rayLength;
        var mapX = (int)Math.Floor(originX);
        var mapY = (int)Math.Floor(originY);
        var deltaX = Math.Abs(rayX) < 0.0000001 ? double.MaxValue : Math.Abs(1 / rayX);
        var deltaY = Math.Abs(rayY) < 0.0000001 ? double.MaxValue : Math.Abs(1 / rayY);
        var stepX = rayX < 0 ? -1 : 1;
        var stepY = rayY < 0 ? -1 : 1;
        var sideX = rayX < 0 ? (originX - mapX) * deltaX : (mapX + 1 - originX) * deltaX;
        var sideY = rayY < 0 ? (originY - mapY) * deltaY : (mapY + 1 - originY) * deltaY;
        var side = 0;
        double distance = 0;

        for (var iteration = 0; iteration < 256 && distance <= maxDistance; iteration++)
        {
            if (sideX < sideY)
            {
                distance = sideX;
                sideX += deltaX;
                mapX += stepX;
                side = 0;
            }
            else
            {
                distance = sideY;
                sideY += deltaY;
                mapY += stepY;
                side = 1;
            }

            if (distance > maxDistance)
            {
                break;
            }

            if (_level.Map.IsSolid(mapX, mapY))
            {
                var perpendicular = side == 0
                    ? (mapX - originX + (1 - stepX) / 2) / rayX
                    : (mapY - originY + (1 - stepY) / 2) / rayY;
                return new RayHit(
                    Math.Max(0.01, perpendicular),
                    Math.Max(0.01, distance),
                    originX + rayX * perpendicular,
                    originY + rayY * perpendicular,
                    _level.Map.TileAt(mapX, mapY),
                    side);
            }
        }

        return new RayHit(maxDistance, maxDistance, originX + rayX * maxDistance, originY + rayY * maxDistance, '#', 0);
    }

    private void SpawnParticles(double x, double y, int count, int color, char glyph, double life)
    {
        for (var index = 0; index < count; index++)
        {
            var angle = _random.NextDouble() * Math.PI * 2;
            var speed = 0.4 + _random.NextDouble() * 2.4;
            var particleLife = life * (0.55 + _random.NextDouble() * 0.7);
            _particles.Add(new Particle(
                x,
                y,
                Math.Cos(angle) * speed,
                Math.Sin(angle) * speed,
                particleLife,
                color,
                glyph));
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    private static void EnableVirtualTerminalProcessing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var output = GetStdHandle(-11);
            if (output == new IntPtr(-1) || !GetConsoleMode(output, out var mode))
            {
                return;
            }

            const uint EnableVirtualTerminalProcessing = 0x0004;
            if ((mode & EnableVirtualTerminalProcessing) == 0)
            {
                SetConsoleMode(output, mode | EnableVirtualTerminalProcessing);
            }
        }
        catch (DllNotFoundException)
        {
            // Le terminal peut etre gere par une couche compatible sans kernel32.
        }
        catch (EntryPointNotFoundException)
        {
            // Idem pour un host terminal non standard.
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetAsyncKeyState")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private void Beep(int frequency, int duration)
    {
        if (!OperatingSystem.IsWindows() || _time - _lastBeepAt < 0.04)
        {
            return;
        }

        _lastBeepAt = _time;
        if (Interlocked.CompareExchange(ref _audioBusy, 1, 0) != 0)
        {
            return;
        }

        // Console.Beep est bloquant sur Windows : on l'execute sur une
        // tache de fond pour ne jamais faire ralentir la boucle de jeu.
        _ = Task.Run(() =>
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Console.Beep(frequency, duration);
                }
            }
            catch (PlatformNotSupportedException)
            {
                // Certains terminaux virtuels n'exposent pas le haut-parleur.
            }
            catch (IOException)
            {
                // Le son est un bonus : une erreur audio ne doit pas stopper le jeu.
            }
            finally
            {
                Volatile.Write(ref _audioBusy, 0);
            }
        });
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle < 0)
        {
            angle += Math.PI * 2;
        }

        while (angle >= Math.PI * 2)
        {
            angle -= Math.PI * 2;
        }

        return angle;
    }

    private static string AmmoLabel(AmmoType ammo) => ammo switch
    {
        AmmoType.Bullets => "BULLETS",
        AmmoType.Shells => "SHELLS",
        AmmoType.Rockets => "ROCKETS",
        AmmoType.Cells => "CELLS",
        _ => "AMMO"
    };

    private readonly record struct RayHit(
        double Distance,
        double RayDistance,
        double X,
        double Y,
        char Tile,
        int Side);
}
