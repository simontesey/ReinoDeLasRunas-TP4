namespace ReinoRunas
{
    public enum Team { Player, Enemy }

    public enum Stat { Attack, Defense, Magic, Speed }

    /// <summary>Estados alterados y estados especiales de combate.</summary>
    public enum StatusType
    {
        None,
        Poison,     // Veneno: pierde vida al comenzar cada turno
        Burn,       // Quemadura: pierde vida y pega menos
        Paralysis,  // Parálisis: puede perder el turno y su barra se llena más lento
        Taunt,      // Provocar: los enemigos están obligados a atacarlo
        Haste,      // Prisa: barra ATB más rápida
        Slow,       // Lentitud: barra ATB más lenta
        Defending   // Defensa: recibe la mitad de daño hasta su próximo turno
    }

    /// <summary>A quién apunta una acción, relativo al equipo de quien la usa.</summary>
    public enum TargetType { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self }

    public enum SkillKind { Physical, Magical, Heal, Support }

    /// <summary>Cómo se anima la acción.</summary>
    public enum SkillAnimation { Melee, Projectile, Cast, Song, Ultimate }

    public enum SkillVisual { Slash, Fire, Lightning, Poison, Holy, Dark, Song, Shield, Meteor }
}
