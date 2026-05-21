namespace SameDaySocialApp.Domain.Enums;

public enum EventType
{
    WORK_STRESS,
    FAMILY,
    FRIENDSHIP,
    RELATIONSHIP,
    STUDY,
    MONEY,
    HEALTH,
    SMALL_HAPPINESS,
    RANDOM
}

public enum EmotionTag
{
    TIRED,
    ANGRY,
    SAD,
    LONELY,
    ANXIOUS,
    HAPPY,
    EXCITED,
    WRONGED,
    CALM
}

public enum ResponseMode
{
    JUST_LISTEN,
    COMFORT_ME,
    GIVE_ADVICE,
    RANT_TOGETHER,
    DISTRACT_ME
}

public enum Visibility
{
    PUBLIC,
    MATCH_ONLY,
    PRIVATE
}

public enum MatchType
{
    SAME_EVENT,
    SAME_EMOTION,
    COMPLEMENTARY,
    SAME_INTEREST,
    TASK_COMPATIBLE
}

public enum RantMode
{
    JUST_SAYING,
    COMFORT_ME,
    GIVE_ADVICE,
    RANT_TOGETHER,
    DISTRACT_ME,
    FIND_SIMILAR
}

public enum TaskCategory
{
    WALK,
    EARLY_SLEEP,
    READ,
    EXERCISE,
    MOVIE,
    STUDY,
    SAVE_MONEY,
    COFFEE,
    BREAKFAST,
    CODING
}
