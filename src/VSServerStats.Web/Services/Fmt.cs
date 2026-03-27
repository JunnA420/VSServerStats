namespace VSServerStats.Web.Services;

public static class Fmt
{
    public static string Playtime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours < 1) return $"{ts.Minutes}m";
        if (ts.TotalDays  < 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{(int)ts.TotalDays}d {ts.Hours}h";
    }

    public static string Distance(double meters)
        => meters >= 1000 ? $"{meters / 1000:0.#} km" : $"{(int)meters} m";

    public static string SkillName(string key) => key switch
    {
        "survival"           => "Přežití",
        "farming"            => "Zemědělství",
        "digging"            => "Kopání",
        "forestry"           => "Lesnictví",
        "mining"             => "Těžba",
        "husbandry"          => "Chov",
        "combat"             => "Boj",
        "metalworking"       => "Kovářství",
        "pottery"            => "Hrnčířství",
        "cooking"            => "Vaření",
        "temporaladaptation" => "Temp. adaptace",
        _                    => key
    };

    public static string SkillIcon(string key) => key switch
    {
        "survival"           => "❤",
        "farming"            => "🌾",
        "digging"            => "⛏",
        "forestry"           => "🪓",
        "mining"             => "⚒",
        "husbandry"          => "🐄",
        "combat"             => "⚔",
        "metalworking"       => "🔨",
        "pottery"            => "🏺",
        "cooking"            => "🍲",
        "temporaladaptation" => "⏳",
        _                    => "✦"
    };

    public static string AbilityName(string key) => key switch
    {
        "longlife"            => "Dlouhý život",
        "hugestomach"         => "Velký žaludek",
        "wellrested"          => "Odpočatý",
        "featherfall"         => "Péřový pád",
        "allrounder"          => "Všestranný",
        "strongback"          => "Silná záda",
        "scout"               => "Průzkumník",
        "healer"              => "Léčitel",
        "steeplechaser"       => "Překážkář",
        "luminiferous"        => "Luminiferous",
        "nudist"              => "Nudista",
        "abundanceadaptation" => "Adaptace hojnosti",
        "greenthumb"          => "Zelený palec",
        "gatherer"            => "Sběrač",
        "carefulhands"        => "Opatrné ruce",
        "farmer"              => "Farmář",
        "demetersbless"       => "Démétřino požehnání",
        "claydigger"          => "Kopač hlíny",
        "shovelexpert"        => "Expert s lopatou",
        "carefuldigger"       => "Opatrný kopač",
        "mixedclay"           => "Smíšená hlína",
        "saltpeterdigger"     => "Kopač ledku",
        "lumberjack"          => "Dřevorubec",
        "afforestation"       => "Zalesňování",
        "treenursery"         => "Lesní školka",
        "charcoalburner"      => "Uhlíř",
        "carefullumberjack"   => "Opatrný dřevorubec",
        "moreladders"         => "Více žebříků",
        "resinfarmer"         => "Pryskyřičář",
        "stonebreaker"        => "Lámač kamene",
        "oreminer"            => "Rudný horník",
        "carefulminer"        => "Opatrný horník",
        "miner"               => "Horník",
        "crystalseeker"       => "Hledač krystalů",
        "bomberman"           => "Minér",
        "butcher"             => "Řezník",
        "rancher"             => "Rančer",
        "hunter"              => "Lovec",
        "swordsman"           => "Šermíř",
        "spearman"            => "Kopinář",
        "looter"              => "Loupežník",
        "toolmastery"         => "Mistr nástrojů",
        "heavyarmorexpert"    => "Expert těžké zbroje",
        "freshflesh"          => "Čerstvé maso",
        "smelter"             => "Tavič",
        "blacksmith"          => "Kovář",
        "metalworker"         => "Kovozpracovatel",
        "metalrecovery"       => "Recyklace kovů",
        "hammerexpert"        => "Expert s kladivem",
        "heatinghits"         => "Žhavé údery",
        "finishingtouch"      => "Poslední dotyk",
        "duplicator"          => "Duplikátor",
        "mastersmith"         => "Mistr kovář",
        "senseoftime"         => "Cit pro čas",
        "machinelearning"     => "Strojové učení",
        "bloomeryexpert"      => "Expert na dmychárnu",
        "automatedsmithing"   => "Automatické kování",
        "thrift"              => "Šetrnost",
        "layerlayer"          => "Vrstva po vrstvě",
        "perfectionist"       => "Perfekcionista",
        "canteencook"         => "Polní kuchař",
        "welldone"            => "Dobře propečené",
        "dilution"            => "Ředění",
        "fastfood"            => "Rychlé jídlo",
        "gourmet"             => "Gurmán",
        "saltybackpack"       => "Solený batoh",
        "temporalstable"      => "Temporální stabilita",
        "temporalrecovery"    => "Temporální regenerace",
        "shifter"             => "Přesouvač",
        "stableminer"         => "Stabilní horník",
        "stablewarrior"       => "Stabilní válečník",
        "caveman"             => "Jeskynní člověk",
        "fastforward"         => "Rychle vpřed",
        _                     => key
    };
}
