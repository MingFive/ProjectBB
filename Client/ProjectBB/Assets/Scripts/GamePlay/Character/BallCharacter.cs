namespace ProjectBB.GamePlay
{
    /// <summary>会话内统一的小球实体，组合运动、承伤和基础命中入口。</summary>
    public sealed class BallCharacter
    {
        public int EntityId { get; }
        public int FactionId { get; }
        public int OwnerId { get; }
        public bool IsSummon => OwnerId != 0;
        public string DefinitionId { get; }
        public double ExpiresAt { get; }
        public BallMotor Motor { get; }
        public CombatManager Combat { get; }
        public SkillManager Skills { get; }
        public TraitManager Traits { get; private set; }
        internal void BindContent(BattleSession session, BattleContent content, BallContentDefinition definition)
        {
            Skills.Bind(session, content);
            Traits = new TraitManager(this, session, definition?.Traits);
        }
        internal BallCharacter(int entityId, int factionId, BallMotor motor, CombatDefinition combat,
            int ownerId = 0, string definitionId = null, double expiresAt = double.PositiveInfinity)
        {
            EntityId = entityId;
            FactionId = factionId;
            OwnerId = ownerId; DefinitionId = definitionId; ExpiresAt = expiresAt;
            Motor = motor;
            Combat = new CombatManager(combat);
            Skills = new SkillManager(this);
        }
    }
}
