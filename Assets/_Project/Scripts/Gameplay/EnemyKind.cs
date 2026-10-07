namespace SquashBot.Gameplay
{
    /// <summary>The moving enemies of the scenario document (see EnemySystem).</summary>
    public enum EnemyKind
    {
        /// <summary>Süpürgeç: a cleaning robot on a rail; it pushes what it bumps a tile along.</summary>
        Sweeper,
        /// <summary>Silgi-bot: a slow robot that turns painted tiles grey again; hop onto it to topple it.</summary>
        Eraser,
        /// <summary>Gözcü dron: patrols, scanning three tiles ahead; stay in its cone and a block comes down.</summary>
        Drone,
        /// <summary>Kum solucanı: a ring swells in the sand, then the worm dives along a three-tile line.</summary>
        Sandworm,
        /// <summary>Yengeç-bot: walks sideways only, changing rows now and then (claws up first).</summary>
        Crab,
        /// <summary>Taret: fixed at the edge, its barrel shows the line its slow shots will fly along.</summary>
        Turret,
        /// <summary>Penguen-bot: slides across the floor (the path is drawn first) and pushes what it meets.</summary>
        Penguin,
        /// <summary>Yay-bot: every two seconds it hops a tile towards the robot; its shadow lands first.</summary>
        Springbot
    }
}
