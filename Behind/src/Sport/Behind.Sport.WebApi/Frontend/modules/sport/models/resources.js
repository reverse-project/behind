define(["modules/sport/models/sport", "modules/sport/models/format", "modules/sport/models/links"], function (sport, format, links) {
    var field = function (type, name, labelKey, extras) {
        return Object.assign({ type: type, name: name, labelKey: labelKey, required: false }, extras);
    };

    var text = function (name, labelKey, extras) {
        return field("text", name, labelKey, extras);
    };

    var textarea = function (name, labelKey, extras) {
        return field("textarea", name, labelKey, extras);
    };

    var number = function (name, labelKey, extras) {
        return field("number", name, labelKey, extras);
    };

    var date = function (name, labelKey, extras) {
        return field("date", name, labelKey, extras);
    };

    var checkbox = function (name, labelKey, extras) {
        return field("checkbox", name, labelKey, extras);
    };

    var choice = function (name, labelKey, options, extras) {
        return field("enum", name, labelKey, Object.assign({ options: options }, extras));
    };

    var image = function (name, labelKey, extras) {
        return field("image", name, labelKey, extras);
    };

    var reference = function (name, labelKey, extras) {
        return field("reference", name, labelKey, extras);
    };

    var withDiscipline = function (from) {
        return function (form) {
            var discipline = from(form);

            return discipline ? { discipline: discipline } : null;
        };
    };

    var chosenDiscipline = withDiscipline(function (form) {
        return form.id("disciplineId");
    });

    var disciplineOf = function (name) {
        return withDiscipline(function (form) {
            var item = form.item(name);

            return item ? item.disciplineId : null;
        });
    };

    var detailItem = function (load, pick) {
        return function (id) {
            return load(id).then(function (result) {
                return result.ok ? pick(result.data) : null;
            });
        };
    };

    var discipline = function () {
        return reference("disciplineId", "sport.field.discipline", {
            required: true,
            target: "disciplines",
            display: "disciplineName"
        });
    };

    var scopeOptions = [
        { value: "Player", labelKey: "sport.scope.player" },
        { value: "Team", labelKey: "sport.scope.team" }
    ];

    var sideOptions = [
        { value: "Left", labelKey: "sport.side.left" },
        { value: "Right", labelKey: "sport.side.right" },
        { value: "Both", labelKey: "sport.side.both" }
    ];

    var cell = function (labelKey, build) {
        return { labelKey: labelKey, cell: build };
    };

    var plain = function (labelKey, pick) {
        return cell(labelKey, function (item) {
            return { text: pick(item) };
        });
    };

    var linked = function (labelKey, kind, idOf, textOf, extras) {
        return cell(labelKey, function (item) {
            return Object.assign({ kind: kind, id: idOf(item), text: textOf(item) }, extras ? extras(item) : {});
        });
    };

    var own = function (labelKey, kind, extras) {
        return linked(labelKey, kind, function (item) {
            return item.id;
        }, function (item) {
            return item.name;
        }, extras);
    };

    var property = function (name) {
        return function (item) {
            return item[name];
        };
    };

    var disciplineCell = function () {
        return linked("sport.field.discipline", "discipline", property("disciplineId"), property("disciplineName"));
    };

    var definitions = [
        {
            name: "disciplines",
            entityType: "Discipline",
            titleKey: "sport.resource.disciplines",
            singularKey: "sport.resource.discipline",
            icon: "fa-futbol",
            publicHref: links.home,
            label: property("name"),
            fields: [
                text("name", "sport.field.name", { required: true, maxLength: 100 }),
                text("slug", "sport.field.slug", {
                    required: true,
                    maxLength: 50,
                    pattern: "[a-z0-9]+(-[a-z0-9]+)*",
                    hintKey: "sport.field.slugHint"
                })
            ],
            columns: [
                own("sport.field.name", "discipline"),
                plain("sport.field.slug", property("slug"))
            ]
        },
        {
            name: "positions",
            entityType: "Position",
            titleKey: "sport.resource.positions",
            singularKey: "sport.resource.position",
            icon: "fa-person-running",
            publicHref: links.players,
            label: property("name"),
            fields: [
                discipline(),
                text("name", "sport.field.name", { required: true, maxLength: 100 }),
                text("abbreviation", "sport.field.abbreviation", { required: true, maxLength: 10 }),
                number("sortOrder", "sport.field.sortOrder", { required: true, initial: "0", step: "1" })
            ],
            columns: [
                own("sport.field.name", "position"),
                plain("sport.field.abbreviation", property("abbreviation")),
                disciplineCell(),
                plain("sport.field.sortOrder", property("sortOrder"))
            ]
        },
        {
            name: "stat-types",
            entityType: "StatType",
            titleKey: "sport.resource.statTypes",
            singularKey: "sport.resource.statType",
            icon: "fa-chart-simple",
            publicHref: links.stats(),
            label: property("name"),
            fields: [
                discipline(),
                text("name", "sport.field.name", { required: true, maxLength: 100 }),
                text("abbreviation", "sport.field.abbreviation", { required: true, maxLength: 10 }),
                choice("scope", "sport.field.scope", scopeOptions, { required: true, initial: "Player" }),
                checkbox("higherIsBetter", "sport.field.higherIsBetter", { initial: true })
            ],
            columns: [
                own("sport.field.name", "statType"),
                plain("sport.field.abbreviation", property("abbreviation")),
                plain("sport.field.scope", function (item) {
                    return format.scope(item.scope);
                }),
                plain("sport.field.higherIsBetter", function (item) {
                    return format.yesNo(item.higherIsBetter);
                }),
                disciplineCell()
            ]
        },
        {
            name: "seasons",
            entityType: "Season",
            titleKey: "sport.resource.seasons",
            singularKey: "sport.resource.season",
            icon: "fa-calendar",
            publicHref: links.seasons,
            label: property("name"),
            fields: [
                discipline(),
                text("name", "sport.field.name", { required: true, maxLength: 100 }),
                date("startsOn", "sport.field.startsOn"),
                date("endsOn", "sport.field.endsOn")
            ],
            columns: [
                own("sport.field.name", "season"),
                disciplineCell(),
                plain("sport.season.period", format.period)
            ]
        },
        {
            name: "teams",
            entityType: "Team",
            titleKey: "sport.resource.teams",
            singularKey: "sport.resource.team",
            icon: "fa-shirt",
            publicHref: links.teams,
            label: property("name"),
            fields: [
                discipline(),
                text("name", "sport.field.name", { required: true, maxLength: 150 }),
                text("shortName", "sport.field.shortName", { maxLength: 20 }),
                text("city", "sport.field.city", { maxLength: 100 }),
                text("country", "sport.field.country", { maxLength: 100 }),
                number("foundedYear", "sport.field.foundedYear", { min: 1800, max: 2200, step: "1" }),
                textarea("description", "sport.field.description", { maxLength: 2000 }),
                image("logoId", "sport.field.logo", { upload: "teams", urlName: "logoUrl", kind: "team" })
            ],
            columns: [
                own("sport.field.name", "team", function (item) {
                    return { image: item.logoUrl, imageKind: "team" };
                }),
                plain("sport.field.shortName", property("shortName")),
                plain("sport.field.city", property("city")),
                plain("sport.field.country", property("country")),
                disciplineCell()
            ]
        },
        {
            name: "players",
            entityType: "Player",
            titleKey: "sport.resource.players",
            singularKey: "sport.resource.player",
            icon: "fa-user",
            publicHref: links.players,
            label: property("name"),
            fields: [
                discipline(),
                text("firstName", "sport.field.firstName", { required: true, maxLength: 100 }),
                text("lastName", "sport.field.lastName", { required: true, maxLength: 100 }),
                date("birthDate", "sport.field.birthDate"),
                text("nationality", "sport.field.nationality", { maxLength: 100 }),
                reference("positionId", "sport.field.position", {
                    target: "positions",
                    display: "positionName",
                    filter: chosenDiscipline
                }),
                number("heightCm", "sport.field.height", { min: 50, max: 300, step: "1" }),
                number("weightKg", "sport.field.weight", { min: 20, max: 400, step: "1" }),
                choice("dominantSide", "sport.field.dominantSide", sideOptions),
                textarea("biography", "sport.field.biography", { maxLength: 4000 })
            ],
            columns: [
                own("sport.field.name", "player", function (item) {
                    return { image: item.pictureUrl, imageKind: "player" };
                }),
                plain("sport.field.age", function (item) {
                    return format.age(item.age);
                }),
                linked("sport.field.position", "position", property("positionId"), property("positionName")),
                linked("sport.field.team", "team", property("teamId"), property("teamName")),
                disciplineCell()
            ]
        },
        {
            name: "player-seasons",
            entityType: "PlayerSeason",
            titleKey: "sport.resource.playerSeasons",
            singularKey: "sport.resource.playerSeason",
            icon: "fa-users",
            publicHref: links.players,
            label: function (item) {
                return item.playerName + " (" + item.seasonName + ")";
            },
            fields: [
                reference("playerId", "sport.field.player", {
                    required: true,
                    target: "players",
                    display: "playerName",
                    hydrate: detailItem(sport.player, function (data) {
                        return data.player;
                    })
                }),
                reference("seasonId", "sport.field.season", {
                    required: true,
                    target: "seasons",
                    display: "seasonName",
                    filter: disciplineOf("playerId")
                }),
                reference("teamId", "sport.field.team", {
                    required: true,
                    target: "teams",
                    display: "teamName",
                    filter: disciplineOf("playerId")
                }),
                number("shirtNumber", "sport.field.shirtNumber", { min: 0, max: 999, step: "1" }),
                image("pictureId", "sport.field.picture", { upload: "players", urlName: "pictureUrl", kind: "player" })
            ],
            columns: [
                linked("sport.field.player", "player", property("playerId"), property("playerName"), function (item) {
                    return { image: item.pictureUrl, imageKind: "player" };
                }),
                linked("sport.field.season", "season", property("seasonId"), property("seasonName")),
                linked("sport.field.team", "team", property("teamId"), property("teamName")),
                linked("sport.field.position", "position", property("positionId"), property("positionName")),
                plain("sport.field.shirtNumber", property("shirtNumber"))
            ]
        },
        {
            name: "stats",
            entityType: "Stat",
            titleKey: "sport.resource.stats",
            singularKey: "sport.resource.stat",
            icon: "fa-ranking-star",
            publicHref: links.stats(),
            label: function (item) {
                return item.statTypeName + " – " + item.subjectName + " (" + item.seasonName + ")";
            },
            fields: [
                reference("seasonId", "sport.field.season", {
                    required: true,
                    target: "seasons",
                    display: "seasonName",
                    hydrate: detailItem(sport.season, function (data) {
                        return data.season;
                    })
                }),
                reference("statTypeId", "sport.field.statType", {
                    required: true,
                    target: "stat-types",
                    display: "statTypeName",
                    filter: disciplineOf("seasonId"),
                    fromRow: function (item) {
                        return { scope: item.scope };
                    }
                }),
                reference("subjectId", "sport.field.subject", {
                    required: true,
                    target: function (form) {
                        var statType = form.item("statTypeId");

                        if (!statType || !statType.scope) {
                            return null;
                        }

                        return statType.scope === "Team" ? "teams" : "players";
                    },
                    display: "subjectName",
                    filter: disciplineOf("seasonId")
                }),
                number("value", "sport.field.value", { required: true, min: -1000000, max: 1000000, step: "any" })
            ],
            columns: [
                linked("sport.field.season", "season", property("seasonId"), property("seasonName")),
                linked("sport.field.statType", "statType", property("statTypeId"), property("statTypeName"), function (item) {
                    return { season: item.seasonId };
                }),
                linked("sport.field.subject", "subject", property("subjectId"), property("subjectName"), function (item) {
                    return { kind: item.scope === "Team" ? "team" : "player" };
                }),
                plain("sport.field.value", function (item) {
                    return format.number(item.value);
                })
            ]
        }
    ];

    return {
        all: definitions,
        find: function (name) {
            return definitions.filter(function (definition) {
                return definition.name === name;
            })[0] || null;
        }
    };
});
