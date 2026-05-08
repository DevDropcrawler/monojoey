namespace MonoJoey.Server.Stats;

internal enum LeaderboardCategory
{
    Wins = 0,
    RentPaid,
    RentCollected,
    AuctionsWon,
    LoansTaken,
    CardsTriggered,
}

internal static class LeaderboardCategoryNames
{
    public static bool TryParse(string value, out LeaderboardCategory category)
    {
        switch (value)
        {
            case "wins":
                category = LeaderboardCategory.Wins;
                return true;
            case "rent_paid":
                category = LeaderboardCategory.RentPaid;
                return true;
            case "rent_collected":
                category = LeaderboardCategory.RentCollected;
                return true;
            case "auctions_won":
                category = LeaderboardCategory.AuctionsWon;
                return true;
            case "loans_taken":
                category = LeaderboardCategory.LoansTaken;
                return true;
            case "cards_triggered":
                category = LeaderboardCategory.CardsTriggered;
                return true;
            default:
                category = default;
                return false;
        }
    }

    public static string ToWireName(LeaderboardCategory category)
    {
        return category switch
        {
            LeaderboardCategory.Wins => "wins",
            LeaderboardCategory.RentPaid => "rent_paid",
            LeaderboardCategory.RentCollected => "rent_collected",
            LeaderboardCategory.AuctionsWon => "auctions_won",
            LeaderboardCategory.LoansTaken => "loans_taken",
            LeaderboardCategory.CardsTriggered => "cards_triggered",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown leaderboard category."),
        };
    }
}
