namespace MonoJoey.Server.GameEngine;

using System.Text.Json;

public static class GameRulesResolver
{
    private static readonly HashSet<string> TopLevelFields = new(StringComparer.Ordinal)
    {
        "version",
        "presetId",
        "presetName",
        "isCustom",
        "economy",
        "auction",
        "jail",
        "dice",
        "cards",
        "loans",
        "win",
        "future",
    };

    private static readonly HashSet<string> KnownDeckIds = new(StringComparer.Ordinal)
    {
        CardDeckIds.Chance,
        CardDeckIds.Table,
    };

    public static GameRules Resolve(JsonElement rulesPayload)
    {
        if (rulesPayload.ValueKind != JsonValueKind.Object)
        {
            throw new GameRulesValidationException("rules must be an object.");
        }

        foreach (var property in rulesPayload.EnumerateObject())
        {
            if (!TopLevelFields.Contains(property.Name))
            {
                throw new GameRulesValidationException($"Unknown rules field '{property.Name}'.");
            }
        }

        var defaultRules = GameRulesPresets.MonoJoeyDefault;
        var requestedPresetId = ReadOptionalString(rulesPayload, "presetId");
        if (requestedPresetId is not null &&
            requestedPresetId != GameRulesPresets.MonoJoeyDefaultPresetId &&
            requestedPresetId != GameRulesPresets.CustomPresetId)
        {
            throw new GameRulesValidationException("Unknown rules preset.");
        }

        if (rulesPayload.TryGetProperty("version", out var versionProperty) &&
            (versionProperty.ValueKind != JsonValueKind.Number ||
                !versionProperty.TryGetInt32(out var version) ||
                version != defaultRules.Version))
        {
            throw new GameRulesValidationException("Unsupported rules version.");
        }

        if (rulesPayload.TryGetProperty("isCustom", out var isCustomProperty) &&
            isCustomProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new GameRulesValidationException("isCustom must be a boolean.");
        }

        var presetName = ReadOptionalString(rulesPayload, "presetName");
        if (presetName is not null)
        {
            presetName = presetName.Trim();
            if (presetName.Length is 0 or > 64)
            {
                throw new GameRulesValidationException("presetName must be between 1 and 64 characters.");
            }
        }

        var economy = rulesPayload.TryGetProperty("economy", out var economyProperty)
            ? MergeEconomy(defaultRules.Economy, economyProperty)
            : defaultRules.Economy;
        var auction = rulesPayload.TryGetProperty("auction", out var auctionProperty)
            ? MergeAuction(defaultRules.Auction, auctionProperty)
            : defaultRules.Auction;
        var jail = rulesPayload.TryGetProperty("jail", out var jailProperty)
            ? MergeJail(defaultRules.Jail, jailProperty)
            : defaultRules.Jail;
        var dice = rulesPayload.TryGetProperty("dice", out var diceProperty)
            ? MergeDice(defaultRules.Dice, diceProperty)
            : defaultRules.Dice;
        var cards = rulesPayload.TryGetProperty("cards", out var cardsProperty)
            ? MergeCards(defaultRules.Cards, cardsProperty)
            : defaultRules.Cards;
        var loans = rulesPayload.TryGetProperty("loans", out var loansProperty)
            ? MergeLoans(defaultRules.Loans, loansProperty)
            : defaultRules.Loans;
        var win = rulesPayload.TryGetProperty("win", out var winProperty)
            ? MergeWin(defaultRules.Win, winProperty)
            : defaultRules.Win;
        var future = rulesPayload.TryGetProperty("future", out var futureProperty)
            ? MergeFuture(defaultRules.Future, futureProperty)
            : defaultRules.Future;

        var resolvedWithoutMetadata = defaultRules with
        {
            Economy = economy,
            Auction = auction,
            Jail = jail,
            Dice = dice,
            Cards = cards,
            Loans = loans,
            Win = win,
            Future = future,
        };
        ValidateResolved(resolvedWithoutMetadata);

        var isCustom = presetName is not null ||
            !RulesMatch(defaultRules, resolvedWithoutMetadata);

        return resolvedWithoutMetadata with
        {
            PresetId = isCustom ? GameRulesPresets.CustomPresetId : GameRulesPresets.MonoJoeyDefaultPresetId,
            PresetName = isCustom ? presetName ?? "Custom" : defaultRules.PresetName,
            IsCustom = isCustom,
        };
    }

    private static EconomyRules MergeEconomy(EconomyRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "economy", new[]
        {
            "startingMoney",
            "passStartReward",
            "incomeTaxAmount",
            "luxuryTaxAmount",
            "baseRentEnabled",
            "upgradesEnabled",
        });

        return baseline with
        {
            StartingMoney = ReadOptionalNonNegativeInt(group, "startingMoney") ?? baseline.StartingMoney,
            PassStartReward = ReadOptionalNonNegativeInt(group, "passStartReward") ?? baseline.PassStartReward,
            IncomeTaxAmount = ReadOptionalNonNegativeInt(group, "incomeTaxAmount") ?? baseline.IncomeTaxAmount,
            LuxuryTaxAmount = ReadOptionalNonNegativeInt(group, "luxuryTaxAmount") ?? baseline.LuxuryTaxAmount,
            BaseRentEnabled = ReadOptionalBool(group, "baseRentEnabled") ?? baseline.BaseRentEnabled,
            UpgradesEnabled = ReadOptionalBool(group, "upgradesEnabled") ?? baseline.UpgradesEnabled,
        };
    }

    private static AuctionRules MergeAuction(AuctionRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "auction", new[]
        {
            "mandatoryAuctionsEnabled",
            "initialTimerSeconds",
            "bidResetTimerSeconds",
            "minimumBidIncrement",
            "startingBid",
        });

        return baseline with
        {
            MandatoryAuctionsEnabled = ReadOptionalBool(group, "mandatoryAuctionsEnabled") ?? baseline.MandatoryAuctionsEnabled,
            InitialTimerSeconds = ReadOptionalPositiveInt(group, "initialTimerSeconds") ?? baseline.InitialTimerSeconds,
            BidResetTimerSeconds = ReadOptionalPositiveInt(group, "bidResetTimerSeconds") ?? baseline.BidResetTimerSeconds,
            MinimumBidIncrement = ReadOptionalPositiveInt(group, "minimumBidIncrement") ?? baseline.MinimumBidIncrement,
            StartingBid = ReadOptionalNonNegativeInt(group, "startingBid") ?? baseline.StartingBid,
        };
    }

    private static JailRules MergeJail(JailRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "jail", new[] { "enabled", "escapeCardsEnabled" });

        return baseline with
        {
            Enabled = ReadOptionalBool(group, "enabled") ?? baseline.Enabled,
            EscapeCardsEnabled = ReadOptionalBool(group, "escapeCardsEnabled") ?? baseline.EscapeCardsEnabled,
        };
    }

    private static DiceRules MergeDice(DiceRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "dice", new[]
        {
            "diceCount",
            "sidesPerDie",
            "resolveLandingAfterCardMove",
        });

        return baseline with
        {
            DiceCount = ReadOptionalPositiveInt(group, "diceCount") ?? baseline.DiceCount,
            SidesPerDie = ReadOptionalAtLeastInt(group, "sidesPerDie", 2) ?? baseline.SidesPerDie,
            ResolveLandingAfterCardMove = ReadOptionalBool(group, "resolveLandingAfterCardMove") ??
                baseline.ResolveLandingAfterCardMove,
        };
    }

    private static CardRules MergeCards(CardRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "cards", new[]
        {
            "decksEnabled",
            "customCardsEnabled",
            "deckEditingEnabled",
        });

        return new CardRules(
            ReadOptionalDeckIds(group, "decksEnabled") ?? baseline.DecksEnabled,
            ReadOptionalBool(group, "customCardsEnabled") ?? baseline.CustomCardsEnabled,
            ReadOptionalBool(group, "deckEditingEnabled") ?? baseline.DeckEditingEnabled);
    }

    private static LoanRules MergeLoans(LoanRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "loans", new[]
        {
            "loanSharkEnabled",
            "baseInterestRate",
            "interestRateIncreasePerLoan",
            "interestRateIncreasePerDebtTier",
            "minimumInterestPayment",
            "canBorrowForLoanPayments",
        });

        return baseline with
        {
            LoanSharkEnabled = ReadOptionalBool(group, "loanSharkEnabled") ?? baseline.LoanSharkEnabled,
            BaseInterestRate = ReadOptionalRate(group, "baseInterestRate") ?? baseline.BaseInterestRate,
            InterestRateIncreasePerLoan = ReadOptionalRate(group, "interestRateIncreasePerLoan") ??
                baseline.InterestRateIncreasePerLoan,
            InterestRateIncreasePerDebtTier = ReadOptionalRate(group, "interestRateIncreasePerDebtTier") ??
                baseline.InterestRateIncreasePerDebtTier,
            MinimumInterestPayment = ReadOptionalNonNegativeInt(group, "minimumInterestPayment") ??
                baseline.MinimumInterestPayment,
            CanBorrowForLoanPayments = ReadOptionalBool(group, "canBorrowForLoanPayments") ??
                baseline.CanBorrowForLoanPayments,
        };
    }

    private static WinRules MergeWin(WinRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "win", new[] { "conditionType" });

        var conditionType = ReadOptionalString(group, "conditionType") ?? baseline.ConditionType;
        if (conditionType != "lastPlayerStanding")
        {
            throw new GameRulesValidationException("Unknown win condition.");
        }

        return baseline with { ConditionType = conditionType };
    }

    private static FutureRules MergeFuture(FutureRules baseline, JsonElement group)
    {
        RequireObjectWithKnownFields(group, "future", new[]
        {
            "slimerEnabled",
            "earthquakeEnabled",
        });

        return baseline with
        {
            SlimerEnabled = ReadOptionalBool(group, "slimerEnabled") ?? baseline.SlimerEnabled,
            EarthquakeEnabled = ReadOptionalBool(group, "earthquakeEnabled") ?? baseline.EarthquakeEnabled,
        };
    }

    private static void RequireObjectWithKnownFields(
        JsonElement group,
        string groupName,
        IEnumerable<string> knownFields)
    {
        if (group.ValueKind != JsonValueKind.Object)
        {
            throw new GameRulesValidationException($"{groupName} rules must be an object.");
        }

        var known = new HashSet<string>(knownFields, StringComparer.Ordinal);
        foreach (var property in group.EnumerateObject())
        {
            if (!known.Contains(property.Name))
            {
                throw new GameRulesValidationException($"Unknown {groupName} rules field '{property.Name}'.");
            }
        }
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new GameRulesValidationException($"{propertyName} must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new GameRulesValidationException($"{propertyName} must be non-empty.");
        }

        return value;
    }

    private static bool? ReadOptionalBool(JsonElement group, string propertyName)
    {
        if (!group.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new GameRulesValidationException($"{propertyName} must be a boolean.");
        }

        return property.GetBoolean();
    }

    private static int? ReadOptionalNonNegativeInt(JsonElement group, string propertyName)
    {
        return ReadOptionalAtLeastInt(group, propertyName, 0);
    }

    private static int? ReadOptionalPositiveInt(JsonElement group, string propertyName)
    {
        return ReadOptionalAtLeastInt(group, propertyName, 1);
    }

    private static int? ReadOptionalAtLeastInt(JsonElement group, string propertyName, int minimumValue)
    {
        if (!group.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value) ||
            value < minimumValue)
        {
            throw new GameRulesValidationException($"{propertyName} is outside the allowed range.");
        }

        return value;
    }

    private static decimal? ReadOptionalRate(JsonElement group, string propertyName)
    {
        if (!group.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetDecimal(out var value) ||
            value < 0m ||
            value > 1m)
        {
            throw new GameRulesValidationException($"{propertyName} must be between 0.0 and 1.0.");
        }

        return value;
    }

    private static IReadOnlyList<string>? ReadOptionalDeckIds(JsonElement group, string propertyName)
    {
        if (!group.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new GameRulesValidationException($"{propertyName} must be an array.");
        }

        var deckIds = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new GameRulesValidationException("Deck IDs must be strings.");
            }

            var deckId = item.GetString();
            if (string.IsNullOrWhiteSpace(deckId) || !KnownDeckIds.Contains(deckId))
            {
                throw new GameRulesValidationException("Unknown deck ID.");
            }

            deckIds.Add(deckId);
        }

        return deckIds.ToArray();
    }

    private static void ValidateResolved(GameRules rules)
    {
        if (rules.Economy.StartingMoney < 0 ||
            rules.Economy.PassStartReward < 0 ||
            rules.Economy.IncomeTaxAmount < 0 ||
            rules.Economy.LuxuryTaxAmount < 0 ||
            rules.Auction.InitialTimerSeconds <= 0 ||
            rules.Auction.BidResetTimerSeconds <= 0 ||
            rules.Auction.MinimumBidIncrement <= 0 ||
            rules.Auction.StartingBid < 0 ||
            rules.Dice.DiceCount <= 0 ||
            rules.Dice.SidesPerDie < 2 ||
            rules.Loans.BaseInterestRate < 0m ||
            rules.Loans.BaseInterestRate > 1m ||
            rules.Loans.InterestRateIncreasePerLoan < 0m ||
            rules.Loans.InterestRateIncreasePerLoan > 1m ||
            rules.Loans.InterestRateIncreasePerDebtTier < 0m ||
            rules.Loans.InterestRateIncreasePerDebtTier > 1m ||
            rules.Loans.MinimumInterestPayment < 0 ||
            rules.Win.ConditionType != "lastPlayerStanding" ||
            rules.Cards.DecksEnabled.Any(deckId => !KnownDeckIds.Contains(deckId)))
        {
            throw new GameRulesValidationException("Rules failed validation.");
        }
    }

    private static bool RulesMatch(GameRules first, GameRules second)
    {
        return first.Economy == second.Economy &&
            first.Auction == second.Auction &&
            first.Jail == second.Jail &&
            first.Dice == second.Dice &&
            first.Loans == second.Loans &&
            first.Win == second.Win &&
            first.Future == second.Future &&
            first.Cards.CustomCardsEnabled == second.Cards.CustomCardsEnabled &&
            first.Cards.DeckEditingEnabled == second.Cards.DeckEditingEnabled &&
            first.Cards.DecksEnabled.SequenceEqual(second.Cards.DecksEnabled, StringComparer.Ordinal);
    }
}
