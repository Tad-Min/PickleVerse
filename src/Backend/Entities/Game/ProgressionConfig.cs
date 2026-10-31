using PickleVerse.Progression;

namespace PickleVerse.Progression;

// Server counterpart of the Unity ScriptableObject, populated exclusively from PostgreSQL.
public sealed class ProgressionConfig
{
    public int winXp, lossXp;
    public RewardBundle dailyBonus, weeklyBonus;
    public List<ProgressDefinition> definitions = [];
}
