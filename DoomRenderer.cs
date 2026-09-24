namespace ConsoleApp1;

internal sealed partial class DoomConsoleGame
{
    private void Render()
    {
        var (width, height) = GetTerminalSize();
        var canvas = new Canvas(width, height);

        if (_screen == GameScreen.Title)
        {
            RenderTitle(canvas);
        }
        else
        {
            RenderWorld(canvas, width, height);
            if (_screen == GameScreen.Map)
            {
                RenderMapOverlay(canvas);
            }
            else if (_screen == GameScreen.Paused || _screen == GameScreen.Dead || _screen == GameScreen.Victory)
            {
                RenderStateOverlay(canvas);
            }
        }

        Console.Write("\x1b[H");
        Console.Write(canvas.Render());
        Console.Write("\x1b[0m\x1b[J");
    }

    private (int Width, int Height) GetTerminalSize()
    {
        var width = _terminalWidth;
        var height = _terminalHeight;
        try
        {
            if (Console.WindowWidth > 0)
            {
                width = Console.WindowWidth;
            }

            if (Console.WindowHeight > 0)
            {
                height = Console.WindowHeight;
            }
        }
        catch (Exception)
        {
            // certaines consoles integration exposent une taille nulle.
        }

        return (Math.Clamp(width, 1, 180), Math.Clamp(height, 1, 70));
    }

    private void RenderTitle(Canvas canvas)
    {
        var width = canvas.Width;
        var height = canvas.Height;
        for (var y = 0; y < height; y++)
        {
            var distance = height <= 1 ? 0 : y / (double)(height - 1);
            var baseColor = distance < 0.48
                ? (y / 3 % 2 == 0 ? 235 : 236)
                : (y / 3 % 2 == 0 ? 238 : 237);
            for (var x = 0; x < width; x++)
            {
                var character = distance < 0.48
                    ? (y % 5 == 0 ? '`' : ' ')
                    : (y % 4 == 0 ? '.' : ' ');
                var color = baseColor;
                if (distance > 0.48 && (x + y * 3 + (int)(_time * 5)) % 97 == 0)
                {
                    character = '.';
                    color = 60;
                }
                else if (distance < 0.48 && (x * 3 + y * 7 + (int)(_time * 2)) % 113 == 0)
                {
                    character = '*';
                    color = 250;
                }

                canvas.Set(x, y, character, color);
            }
        }

        DrawBox(canvas, 2, 2, width - 5, height - 5, 88);
        DrawBox(canvas, 3, 3, width - 7, height - 7, 238);

        var title = "DOOM // CONSOLE";
        canvas.DrawTextCentered(5, title, 231, 52);
        canvas.DrawTextCentered(7, "P R O C E D U R A L   F A N - M A D E   F P S", 214, 236);
        canvas.DrawHorizontalLine(9, 5, width - 6, '─', 88, 236);

        canvas.DrawTextCentered(11, "3 SECTEURS  //  5 ARMES  //  1 OBJECTIF", 250, 236);
        canvas.DrawTextCentered(13, "ENTRE : DEBUTER", 231, 236);
        canvas.DrawTextCentered(15, "Z Q S D / W A S D : DEPLACER    FLECHES : VISEE", 250, 236);
        canvas.DrawTextCentered(16, "ESPACE : TIRER    E : PORTE    M : CARTE    P : PAUSE", 250, 236);
        canvas.DrawTextCentered(17, "1-5 : ARMES    X : QUITTER", 250, 236);
        canvas.DrawTextCentered(19, "RAYCASTING + BILLBOARDS ASCII // AUCUN ASSET EXTERNE", 111, 236);

        if ((int)(_time * 2) % 2 == 0)
        {
            canvas.DrawTextCentered(Math.Min(height - 5, 23), ">>> APPUIE SUR ENTREE <<<", 226, 236);
        }

        if (width > 78)
        {
            canvas.DrawText(6, height - 4, "BUILD 2026.09", 244, 236);
            canvas.DrawText(width - 22, height - 4, "FAIT POUR LE TERMINAL", 244, 236);
        }
    }

    private void RenderWorld(Canvas canvas, int width, int height)
    {
        if (_level is null || _player is null)
        {
            return;
        }

        var shake = (int)Math.Round(Math.Sin(_time * 91.0) * _player.ScreenShake * 3.0);
        var pitch = (int)Math.Round(Math.Sin(_player.WalkTime * 1.8) * 2.2 + _player.WeaponSway * 2.5) + shake;
        var horizon = Math.Clamp(height / 2 + pitch, height / 5, height * 4 / 5);
        RenderEnvironment(canvas, horizon);
        var depthBuffer = RenderWalls(canvas, width, height, horizon);
        RenderBillboards(canvas, width, height, horizon, depthBuffer);
        RenderProjectileSprites(canvas, width, height, horizon, depthBuffer);
        RenderParticleSprites(canvas, width, height, horizon, depthBuffer);

        if (_player.DamageFlash > 0)
        {
            canvas.Tint(196, 52, (int)(_time * 20));
            DrawHorizontalBorder(canvas, 196, 52);
        }

        RenderCrosshair(canvas, width, height);
        RenderHud(canvas, width, height);
    }

    private static void RenderEnvironment(Canvas canvas, int horizon)
    {
        var width = canvas.Width;
        var height = canvas.Height;
        for (var y = 0; y < height; y++)
        {
            var isFloor = y >= horizon;
            var relative = isFloor
                ? (y - horizon) / (double)Math.Max(1, height - horizon)
                : (horizon - y) / (double)Math.Max(1, horizon);
            for (var x = 0; x < width; x++)
            {
                var checker = ((x / 4) + (y / 3)) & 1;
                char character;
                int color;
                if (isFloor)
                {
                    character = relative < 0.16 ? '=' : checker == 0 ? '.' : '·';
                    color = relative < 0.16 ? 94 : checker == 0 ? 239 : 237;
                    if ((x + y * 2) % 31 == 0)
                    {
                        character = '|';
                        color = 240;
                    }
                }
                else
                {
                    character = relative < 0.12 ? '`' : checker == 0 ? ' ' : '.';
                    color = relative < 0.12 ? 238 : checker == 0 ? 235 : 236;
                    if ((x * 7 + y * 11) % 89 == 0)
                    {
                        character = '*';
                        color = 60;
                    }
                }

                canvas.Set(x, y, character, color);
            }
        }

        // Une ligne d'horizon donne du relief meme sans lumieres.
        canvas.DrawHorizontalLine(Math.Clamp(horizon, 0, canvas.Height - 1), 0, canvas.Width - 1, '·', 240, -1);
    }

    private double[] RenderWalls(Canvas canvas, int width, int height, int horizon)
    {
        var depthBuffer = new double[width];
        var directionX = Math.Cos(_player.Angle);
        var directionY = Math.Sin(_player.Angle);
        var planeX = -directionY * FieldOfView;
        var planeY = directionX * FieldOfView;

        for (var x = 0; x < width; x++)
        {
            var cameraX = 2.0 * x / Math.Max(1, width - 1) - 1;
            var rayX = directionX + planeX * cameraX;
            var rayY = directionY + planeY * cameraX;
            var length = Math.Sqrt(rayX * rayX + rayY * rayY);
            rayX /= length;
            rayY /= length;
            var hit = CastRay(_player.X, _player.Y, rayX, rayY, 42);
            depthBuffer[x] = hit.Distance;

            var wallHeight = Math.Min(height * 3.2, height * 1.08 / Math.Max(0.04, hit.Distance));
            var top = horizon - (int)(wallHeight / 2);
            var bottom = horizon + (int)(wallHeight / 2);
            var wallCoordinate = hit.Side == 0
                ? hit.Y
                : hit.X;
            wallCoordinate -= Math.Floor(wallCoordinate);
            var texture = (int)(wallCoordinate * 10);
            var baseColor = WallColor(hit.Tile, hit.Side, hit.Distance);
            var glyph = WallGlyph(hit.Tile, hit.Side, texture);
            var start = Math.Max(0, top);
            var end = Math.Min(height - 1, bottom);
            for (var y = start; y <= end; y++)
            {
                var rowTexture = (y + texture) & 7;
                var rowGlyph = glyph;
                if (hit.Tile is '#' or 'C')
                {
                    rowGlyph = rowTexture switch
                    {
                        0 => '▓',
                        1 => '█',
                        2 => '▒',
                        3 => '█',
                        4 => '▓',
                        5 => '█',
                        6 => '▒',
                        _ => '█'
                    };
                }
                else if (hit.Tile == 'B' && rowTexture % 3 == 0)
                {
                    rowGlyph = ' ';
                }
                else if (hit.Tile == 'D' && rowTexture % 4 == 0)
                {
                    rowGlyph = '╬';
                }

                var color = baseColor;
                if (rowGlyph == ' ')
                {
                    color = 232;
                }
                else if ((y + texture) % 17 == 0)
                {
                    color = Math.Max(0, baseColor - 5);
                }

                canvas.Set(x, y, rowGlyph, color);
            }

            if (hit.Distance < 1.2)
            {
                for (var y = Math.Max(0, top); y <= Math.Min(height - 1, bottom); y++)
                {
                    canvas.Set(x, y, '█', 236);
                }
            }
        }

        return depthBuffer;
    }

    private static int WallColor(char tile, int side, double distance)
    {
        var shade = Math.Clamp(1.0 - distance / 24.0, 0.0, 1.0);
        int color;
        if (tile == 'A')
        {
            color = 94;
        }
        else if (tile == 'B')
        {
            color = 130;
        }
        else if (tile == 'C')
        {
            color = 180;
        }
        else if (tile == 'X')
        {
            color = 60;
        }
        else if (tile == 'D')
        {
            color = 208;
        }
        else
        {
            color = 244;
        }

        if (side == 1)
        {
            shade *= 0.78;
        }

        // ANSI 256: les gris de la rampe 232..255 sont suffisants pour
        // produire une profondeur stable sans dependre d'une image.
        if (tile is '#' or 'C' or 'D')
        {
            var gray = 232 + (int)Math.Round(shade * 23);
            if (tile == 'C')
            {
                gray = Math.Max(0, gray - 5);
            }
            else if (tile == 'D')
            {
                gray = 172 + (int)Math.Round(shade * 25);
            }

            return gray;
        }

        return color;
    }

    private static char WallGlyph(char tile, int side, int texture)
    {
        if (tile == 'A')
        {
            return '█';
        }

        if (tile == 'B')
        {
            return texture % 2 == 0 ? '▓' : '▒';
        }

        if (tile == 'C')
        {
            return texture % 3 == 0 ? '▒' : '█';
        }

        if (tile == 'X')
        {
            return '╬';
        }

        if (tile == 'D')
        {
            return '▥';
        }

        return side == 0 ? '║' : '█';
    }

    private void RenderBillboards(Canvas canvas, int width, int height, int horizon, double[] depthBuffer)
    {
        if (_level is null)
        {
            return;
        }

        var billboards = new List<Billboard>();
        foreach (var enemy in _level.Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            var definition = enemy.Definition;
            var hitFlash = enemy.HitFlash > 0;
            billboards.Add(new Billboard(
                enemy.X,
                enemy.Y,
                definition.Scale,
                hitFlash ? 231 : definition.Color,
                hitFlash ? 255 : definition.AccentColor,
                definition.Pattern,
                true,
                enemy.AnimationTime,
                enemy.Definition.MaxHealth,
                enemy.Health));
        }

        foreach (var pickup in _level.Pickups)
        {
            if (!pickup.Active)
            {
                continue;
            }

            var (color, accent, pattern) = PickupVisual(pickup.Kind);
            billboards.Add(new Billboard(pickup.X, pickup.Y, 0.42, color, accent, pattern, false, 0, 0, 0));
        }

        var exit = _level.Exit;
        var exitPulse = (int)(Math.Sin(_time * 3.5) * 2);
        billboards.Add(new Billboard(
            exit.X + 0.5,
            exit.Y + 0.5,
            0.72,
            46 + exitPulse,
            40,
            new[] { " ##### ", " # E E #", " # E E #", " ##### ", "  ###  " },
            false,
            _time,
            0,
            0));

        // Le tri par profondeur approximative est suffisant pour un
        // billboard console et evite un moteur 3D complet.
        billboards.Sort((first, second) =>
        {
            var firstDistance = DistanceSquared(_player.X, _player.Y, first.X, first.Y);
            var secondDistance = DistanceSquared(_player.X, _player.Y, second.X, second.Y);
            return secondDistance.CompareTo(firstDistance);
        });

        foreach (var billboard in billboards)
        {
            RenderBillboard(canvas, width, height, horizon, depthBuffer, billboard);
        }
    }

    private void RenderBillboard(Canvas canvas, int width, int height, int horizon, double[] depthBuffer, Billboard billboard)
    {
        var projection = Project(billboard.X, billboard.Y, width, height);
        if (projection is null)
        {
            return;
        }

        var (screenX, depth) = projection.Value;
        var spriteHeight = Math.Max(2, (int)(height * billboard.Scale / depth));
        var spriteWidth = Math.Max(2, (int)(spriteHeight * 0.78));
        if (depth < 0.16 || spriteWidth >= width * 4)
        {
            return;
        }

        var bob = (int)Math.Round(Math.Sin(_time * 4.2 + billboard.Phase) * Math.Min(2, spriteHeight * 0.12));
        var bottom = Math.Clamp(
            horizon + (int)(height * 0.52 / depth) + bob,
            Math.Min(1, height - 1),
            height - 1);
        var left = screenX - spriteWidth / 2;
        var top = bottom - spriteHeight;
        if (left > width || left + spriteWidth < 0 || top > height || top + spriteHeight < 0)
        {
            return;
        }

        // Ombre portee au sol.
        var shadowWidth = Math.Max(1, spriteWidth);
        for (var x = Math.Max(0, left); x <= Math.Min(width - 1, left + shadowWidth); x++)
        {
            if (depth <= depthBuffer[x])
            {
                canvas.Set(x, Math.Min(height - 1, bottom), '▄', 235);
            }
        }

        var pattern = billboard.Pattern;
        var patternHeight = pattern.Length;
        var patternWidth = pattern.Max(row => row.Length);
        var cellHeight = Math.Max(1, spriteHeight / patternHeight);
        var cellWidth = Math.Max(1, spriteWidth / patternWidth);
        for (var row = 0; row < patternHeight; row++)
        {
            var line = pattern[row];
            for (var column = 0; column < line.Length; column++)
            {
                var character = line[column];
                if (character == ' ')
                {
                    continue;
                }

                var foreground = billboard.IsEnemy
                    ? (character is '^' or 'v' or '/' or '\\' or '|' ? billboard.AccentColor : billboard.Color)
                    : billboard.Color;
                var cellX = left + column * cellWidth;
                var cellY = top + row * cellHeight;
                var cellRight = Math.Min(left + spriteWidth, cellX + cellWidth);
                var cellBottom = Math.Min(bottom, cellY + cellHeight);
                for (var y = Math.Max(0, cellY); y < cellBottom; y++)
                {
                    for (var x = Math.Max(0, cellX); x < cellRight; x++)
                    {
                        if (x < width && depth <= depthBuffer[x])
                        {
                            canvas.Set(x, y, character, foreground);
                        }
                    }
                }
            }
        }

        if (billboard.IsEnemy && depth < 6 && billboard.MaxHealth > 0)
        {
            var barWidth = Math.Max(5, Math.Min(18, spriteWidth));
            var maxBarLeft = Math.Max(0, width - barWidth - 1);
            var barLeft = Math.Clamp(screenX - barWidth / 2, 0, maxBarLeft);
            var barY = Math.Max(0, top - 1);
            var filled = (int)Math.Round(barWidth * Math.Clamp((double)billboard.Health / billboard.MaxHealth, 0, 1));
            var visibleBarWidth = Math.Min(barWidth, Math.Max(0, width - barLeft));
            for (var x = 0; x < visibleBarWidth; x++)
            {
                var barScreenX = barLeft + x;
                if (barScreenX >= 0 && barScreenX < width && depth <= depthBuffer[barScreenX])
                {
                    canvas.Set(barScreenX, barY, x < filled ? '█' : '░', x < filled ? 46 : 240);
                }
            }
        }
    }

    private void RenderProjectileSprites(Canvas canvas, int width, int height, int horizon, double[] depthBuffer)
    {
        foreach (var projectile in _projectiles)
        {
            var projection = Project(projectile.X, projectile.Y, width, height);
            if (projection is null)
            {
                continue;
            }

            var (screenX, depth) = projection.Value;
            var size = depth < 0.6 ? 3 : depth < 1.5 ? 2 : 1;
            for (var dy = -size / 2; dy <= size / 2; dy++)
            {
                for (var dx = -size / 2; dx <= size / 2; dx++)
                {
                    var x = screenX + dx;
                    var y = horizon + (int)(height * 0.5 / depth) + dy;
                    if (x >= 0 && x < width && y >= 0 && y < height && depth <= depthBuffer[x])
                    {
                        canvas.Set(x, y, projectile.Owner == ProjectileOwner.Enemy ? 'o' : '*', projectile.Color);
                    }
                }
            }
        }
    }

    private void RenderParticleSprites(Canvas canvas, int width, int height, int horizon, double[] depthBuffer)
    {
        foreach (var particle in _particles)
        {
            var projection = Project(particle.X, particle.Y, width, height);
            if (projection is null)
            {
                continue;
            }

            var (screenX, depth) = projection.Value;
            var y = horizon + (int)(height * 0.5 / depth);
            if (screenX >= 0 && screenX < width && y >= 0 && y < height && depth <= depthBuffer[screenX])
            {
                var color = particle.Life < particle.MaxLife * 0.35 ? 231 : particle.Color;
                canvas.Set(screenX, y, particle.Glyph, color);
            }
        }
    }

    private (int ScreenX, double Depth)? Project(double worldX, double worldY, int width, int height)
    {
        var directionX = Math.Cos(_player.Angle);
        var directionY = Math.Sin(_player.Angle);
        var planeX = -directionY * FieldOfView;
        var planeY = directionX * FieldOfView;
        var relativeX = worldX - _player.X;
        var relativeY = worldY - _player.Y;
        var inverse = 1.0 / (planeX * directionY - directionX * planeY);
        var transformX = inverse * (directionY * relativeX - directionX * relativeY);
        var transformY = inverse * (-planeY * relativeX + planeX * relativeY);
        if (transformY <= 0.08)
        {
            return null;
        }

        var screenX = (int)((width / 2.0) * (1 + transformX / transformY));
        if (screenX < -width || screenX > width * 2)
        {
            return null;
        }

        return (screenX, transformY);
    }

    private void RenderCrosshair(Canvas canvas, int width, int height)
    {
        var centerX = width / 2;
        var centerY = height / 2;
        var color = _player.MuzzleFlash > 0 ? 231 : 250;
        canvas.Set(centerX, centerY, '+', color);
        canvas.Set(centerX - 1, centerY, '─', color);
        canvas.Set(centerX + 1, centerY, '─', color);
        canvas.Set(centerX, centerY - 1, '│', color);
        canvas.Set(centerX, centerY + 1, '│', color);
    }

    private void RenderHud(Canvas canvas, int width, int height)
    {
        if (_level is null)
        {
            return;
        }

        var top = Math.Max(0, height - 6);
        canvas.FillRect(0, 0, width, 1, ' ', 250, 236);
        var remainingEnemies = _level.Enemies.Count(enemy => enemy.Alive);
        var objective = _level.RequiresClear && remainingEnemies > 0
            ? $"NETTOIE LE SECTEUR ({remainingEnemies})"
            : _level.RequiresKey && !_player.HasKey
                ? "TROUVE LA CLE"
                : "ATTEINS LA SORTIE";
        canvas.DrawText(1, 0, $"[{_levelIndex + 1}/{_levels.Count}] {_level.Name}  //  {objective}", 229, 236);

        canvas.FillRect(0, top, width, height - top, ' ', 250, 236);
        canvas.DrawHorizontalLine(top, 0, width - 1, '─', 88, 236);

        var healthText = $"HP {_player.Health:000}";
        var armorText = $"ARMOR {_player.Armor:000}";
        canvas.DrawText(2, top + 1, healthText, 231, 236);
        DrawBar(canvas, 8, top + 1, Math.Max(8, Math.Min(20, width / 5)), _player.Health, 196, 236);
        canvas.DrawText(31, top + 1, armorText, 81, 236);
        DrawBar(canvas, 40, top + 1, Math.Max(6, Math.Min(14, width / 7)), _player.Armor, 81, 236);

        var weapon = _player.CurrentWeaponDefinition;
        var ammo = weapon.UsesAmmo ? $"{_player.Ammo[weapon.Ammo!.Value]:000}" : "INF";
        var ammoLabel = weapon.UsesAmmo ? AmmoLabel(weapon.Ammo!.Value) : "RESERVE";
        var rightText = $"{weapon.ShortName}  {ammo}  {ammoLabel}";
        if (rightText.Length < width - 43)
        {
            canvas.DrawText(width - rightText.Length - 2, top + 1, rightText, 226, 236);
        }

        canvas.DrawText(2, top + 2, $"SCORE {_player.Score:000000}    ELIMINATIONS {_player.Kills:00}", 250, 236);
        if (_player.MessageTime > 0)
        {
            var messageColor = _player.HasKey ? 46 : 226;
            canvas.DrawTextCentered(top + 3, _player.Message, messageColor, 236);
        }

        var controls = "ZQSD/WASD MOUVEMENT   ESPACE TIR   E PORTE   M CARTE   P PAUSE   X SORTIE";
        if (controls.Length > width - 2)
        {
            controls = "ZQSD MOUV   ESP TIR   E PORTE   M CARTE   P PAUSE   X SORTIE";
        }

        canvas.DrawTextCentered(height - 1, controls, 244, 236);
        RenderWeaponGraphic(canvas, width, height, top);
    }

    private static void DrawBar(Canvas canvas, int x, int y, int width, int value, int color, int background)
    {
        var filled = (int)Math.Round(width * Math.Clamp(value / 100.0, 0, 1));
        for (var index = 0; index < width; index++)
        {
            canvas.Set(x + index, y, index < filled ? '█' : '░', index < filled ? color : 244, background);
        }
    }

    private void RenderWeaponGraphic(Canvas canvas, int width, int height, int hudTop)
    {
        var weapon = _player.CurrentWeapon;
        string[] art = weapon switch
        {
            WeaponType.Pistol => new[] { "  __  ", " /__\\ ", "|    |", "|____|", "  ||  " },
            WeaponType.Shotgun => new[] { " |====|", " |====|", " |    |", "  |  | ", "  |__| " },
            WeaponType.Chaingun => new[] { "  |||| ", " /_||_\\", " |    |", " |    |", "  |__| " },
            WeaponType.RocketLauncher => new[] { "  ==== ", " [====]", " |    |", " |    |", "  |__| " },
            _ => new[] { "  .--. ", " / || \\", " | || |", " \\ || /", "  '--' " }
        };

        var artWidth = art.Max(row => row.Length);
        var left = Math.Max(2, width / 2 - artWidth / 2 + (int)Math.Round(Math.Sin(_player.WalkTime) * 2));
        var top = Math.Max(2, hudTop - art.Length - 1);
        for (var row = 0; row < art.Length; row++)
        {
            for (var column = 0; column < art[row].Length; column++)
            {
                var x = left + column;
                var y = top + row;
                if (x < 0 || x >= width || y < 0 || y >= hudTop)
                {
                    continue;
                }

                var color = art[row][column] switch
                {
                    '_' or '|' or '/' or '\\' => 250,
                    '=' => 240,
                    _ => 88
                };
                canvas.Set(x, y, art[row][column], color, 236);
            }
        }

        if (_player.MuzzleFlash > 0)
        {
            var muzzleX = Math.Max(0, Math.Min(width - 1, width / 2));
            var muzzleY = Math.Max(1, top - 1);
            canvas.DrawText(muzzleX - 1, muzzleY, "✹", 231, 236);
        }
    }

    private void RenderMapOverlay(Canvas canvas)
    {
        if (_level is null)
        {
            return;
        }

        var width = canvas.Width;
        var height = canvas.Height;
        canvas.Tint(240, 235, 0);
        var mapWidth = _level.Map.Width;
        var mapHeight = _level.Map.Height;
        var mapDisplayHeight = (mapHeight + 1) / 2;
        var panelWidth = Math.Max(1, Math.Min(width - 4, mapWidth + 4));
        var panelHeight = Math.Max(1, Math.Min(height - 2, mapDisplayHeight + 7));
        var left = Math.Max(0, (width - panelWidth) / 2);
        var top = Math.Max(0, (height - panelHeight) / 2);
        canvas.FillRect(left, top, panelWidth, panelHeight, ' ', 250, 236);
        DrawBox(canvas, left, top, panelWidth - 1, panelHeight - 1, 88);
        canvas.DrawText(left + 2, top + 1, $"CARTE // {_level.Name}", 226, 236);
        canvas.DrawText(left + 2, top + 2, "▲ TOI   E SORTIE   + SANTE   K CLE   D PORTE", 244, 236);

        var originX = left + 2;
        var originY = top + 4;
        // Deux lignes de la carte sont combinees dans un demi-bloc : la
        // carte complete reste visible dans un terminal 80x24.
        for (var compactY = 0; compactY < mapDisplayHeight && originY + compactY < height - 2; compactY++)
        {
            var topMapY = compactY * 2;
            var bottomMapY = topMapY + 1;
            for (var x = 0; x < _level.Map.Width && originX + x < width - 1; x++)
            {
                var topTile = MapTileVisual(_level, x, topMapY);
                var bottomTile = bottomMapY < mapHeight
                    ? MapTileVisual(_level, x, bottomMapY)
                    : (' ', 236);
                canvas.Set(originX + x, originY + compactY, '▀', topTile.Item2, bottomTile.Item2);
            }
        }

        foreach (var enemy in _level.Enemies)
        {
            if (enemy.Alive)
            {
                var x = originX + (int)enemy.X;
                var y = originY + (int)enemy.Y / 2;
                canvas.Set(x, y, enemy.Kind == EnemyKind.Boss ? 'Ω' : 'x', 196, 236);
            }
        }

        foreach (var pickup in _level.Pickups)
        {
            if (!pickup.Active)
            {
                continue;
            }

            var (character, color) = pickup.Kind switch
            {
                PickupKind.Health => ('+', 46),
                PickupKind.Armor => ('A', 81),
                PickupKind.Key => ('K', 226),
                PickupKind.Weapon => ('W', 214),
                _ => ('a', 250)
            };
            canvas.Set(originX + (int)pickup.X, originY + (int)pickup.Y / 2, character, color, 236);
        }

        var playerX = originX + (int)_player.X;
        var playerY = originY + (int)_player.Y / 2;
        canvas.Set(playerX, playerY, '▲', 46, 236);
        canvas.DrawTextCentered(height - 2, "M / P : RETOUR", 244, 236);
    }

    private static (char Character, int Color) MapTileVisual(Level level, int x, int y)
    {
        var tile = level.Map.TileAt(x, y);
        return tile switch
        {
            '#' => ('█', 244),
            'X' => ('╬', 60),
            'B' => ('▓', 130),
            'C' => ('▒', 180),
            'A' => ('█', 94),
            'D' => (closestDoorOpen(level, x, y) ? '▫' : '▥', 208),
            'E' => ('E', 46),
            _ => ('·', 238)
        };
    }

    private static bool closestDoorOpen(Level level, int x, int y) => level.Map.GetDoor(x, y)?.Open == true;

    private void RenderStateOverlay(Canvas canvas)
    {
        canvas.Tint(240, 235, 1);
        var width = canvas.Width;
        var height = canvas.Height;
        var panelWidth = Math.Max(1, Math.Min(width - 8, 68));
        var panelHeight = Math.Max(1, Math.Min(height - 4, 15));
        var left = (width - panelWidth) / 2;
        var top = (height - panelHeight) / 2;
        canvas.FillRect(left, top, panelWidth, panelHeight, ' ', 250, 236);
        DrawBox(canvas, left, top, panelWidth - 1, panelHeight - 1, 88);

        if (_screen == GameScreen.Paused)
        {
            canvas.DrawTextCentered(top + 2, _levelTransition ? "SECTEUR NETTOYE" : "PAUSE", 226, 236);
            canvas.DrawTextCentered(top + 5, _levelTransition ? "ENTREE / P : SECTEUR SUIVANT" : "P : REPRENDRE", 250, 236);
            canvas.DrawTextCentered(top + 6, "X / ECHAP : QUITTER", 250, 236);
        }
        else if (_screen == GameScreen.Dead)
        {
            canvas.DrawTextCentered(top + 2, "TU ES MORT", 196, 236);
            canvas.DrawTextCentered(top + 5, $"SCORE {_player.Score:000000}   //   ELIMINATIONS {_player.Kills}", 250, 236);
            canvas.DrawTextCentered(top + 8, "ENTREE : REJOUER    X : QUITTER", 250, 236);
        }
        else if (_screen == GameScreen.Victory)
        {
            canvas.DrawTextCentered(top + 2, "VICTOIRE", 46, 236);
            canvas.DrawTextCentered(top + 5, "LE SIGNAL EST LIBRE.", 250, 236);
            canvas.DrawTextCentered(top + 6, $"SCORE FINAL {_player.Score:000000}", 226, 236);
            canvas.DrawTextCentered(top + 9, "ENTREE : NOUVELLE PARTIE    X : QUITTER", 250, 236);
        }
    }

    private static void DrawHorizontalBorder(Canvas canvas, int foreground, int background)
    {
        for (var x = 0; x < canvas.Width; x++)
        {
            canvas.Set(x, 0, '█', foreground, background);
            canvas.Set(x, canvas.Height - 1, '█', foreground, background);
        }

        for (var y = 0; y < canvas.Height; y++)
        {
            canvas.Set(0, y, '█', foreground, background);
            canvas.Set(canvas.Width - 1, y, '█', foreground, background);
        }
    }

    private static void DrawBox(Canvas canvas, int left, int top, int right, int bottom, int color)
    {
        for (var x = Math.Max(0, left); x <= Math.Min(canvas.Width - 1, right); x++)
        {
            canvas.Set(x, Math.Max(0, top), '─', color);
            canvas.Set(x, Math.Min(canvas.Height - 1, bottom), '─', color);
        }

        for (var y = Math.Max(0, top); y <= Math.Min(canvas.Height - 1, bottom); y++)
        {
            canvas.Set(Math.Max(0, left), y, '│', color);
            canvas.Set(Math.Min(canvas.Width - 1, right), y, '│', color);
        }

        if (left >= 0 && top >= 0 && left < canvas.Width && top < canvas.Height)
        {
            canvas.Set(left, top, '┌', color);
        }

        if (right >= 0 && top >= 0 && right < canvas.Width && top < canvas.Height)
        {
            canvas.Set(right, top, '┐', color);
        }

        if (left >= 0 && bottom >= 0 && left < canvas.Width && bottom < canvas.Height)
        {
            canvas.Set(left, bottom, '└', color);
        }

        if (right >= 0 && bottom >= 0 && right < canvas.Width && bottom < canvas.Height)
        {
            canvas.Set(right, bottom, '┘', color);
        }
    }

    private static double DistanceSquared(double x1, double y1, double x2, double y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private readonly record struct Billboard(
        double X,
        double Y,
        double Scale,
        int Color,
        int AccentColor,
        string[] Pattern,
        bool IsEnemy,
        double Phase,
        int MaxHealth,
        int Health);

    private static (int Color, int Accent, string[] Pattern) PickupVisual(PickupKind kind)
    {
        return kind switch
        {
            PickupKind.Health => (46, 226, new[] { " + ", "+++", " + " }),
            PickupKind.Armor => (81, 159, new[] { "[ ]", "[-]", "[ ]" }),
            PickupKind.Ammo => (250, 214, new[] { " • ", "•••", " • " }),
            PickupKind.Weapon => (214, 231, new[] { " W ", "WWW", " W " }),
            PickupKind.Key => (226, 46, new[] { " K ", "╔═╗", " K " }),
            _ => (46, 231, new[] { " E ", "║║", " E " })
        };
    }
}
