define(["models/i18n"], function (i18n) {
    var sides = {
        Left: "sport.side.left",
        Right: "sport.side.right",
        Both: "sport.side.both"
    };

    var scopes = {
        Player: "sport.scope.player",
        Team: "sport.scope.team"
    };

    var date = function (value) {
        return value ? i18n.date(new Date(value + "T00:00:00")) : "";
    };

    var present = function (value) {
        return value !== null && value !== undefined && value !== "";
    };

    var translated = function (keys) {
        return function (value) {
            return keys[value] ? i18n.t(keys[value]) : "";
        };
    };

    var startOf = function (season) {
        return season.startsOn || "";
    };

    return {
        date: date,
        present: present,
        side: translated(sides),
        scope: translated(scopes),
        yesNo: function (value) {
            return i18n.t(value ? "sport.yes" : "sport.no");
        },
        number: function (value) {
            return present(value)
                ? Number(value).toLocaleString(i18n.locale(), { maximumFractionDigits: 2 })
                : "";
        },
        period: function (season) {
            return [date(season.startsOn), date(season.endsOn)].filter(Boolean).join(" – ");
        },
        age: function (value) {
            return present(value) ? i18n.t("sport.unit.years", [value]) : "";
        },
        height: function (value) {
            return present(value) ? i18n.t("sport.unit.cm", [value]) : "";
        },
        weight: function (value) {
            return present(value) ? i18n.t("sport.unit.kg", [value]) : "";
        },
        lines: function (text) {
            return String(text || "")
                .split(/\r?\n/)
                .map(function (line) {
                    return line.trim();
                })
                .filter(Boolean);
        },
        newestFirst: function (seasons) {
            return seasons.slice().sort(function (left, right) {
                return startOf(right).localeCompare(startOf(left)) || right.name.localeCompare(left.name);
            });
        }
    };
});
