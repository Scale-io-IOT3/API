CREATE TABLE User
(
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT NOT NULL,
    password_hash TEXT NOT NULL
);

CREATE TABLE Meal
(
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id    INTEGER NOT NULL,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES User (id)
);


CREATE TABLE Food
(
    id       TEXT PRIMARY KEY,
    meal_id  INTEGER NOT NULL,
    quantity REAL    NOT NULL,
    calories INTEGER,
    name     TEXT    NOT NULL,
    brands   TEXT,

    FOREIGN KEY (meal_id) REFERENCES Meal (id)
);

CREATE TABLE Macros
(
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    food_id       TEXT NOT NULL,
    macro_type_id INTEGER NOT NULL,
    amount        REAL    NOT NULL,
    percentage    REAL    NOT NULL,

    FOREIGN KEY (food_id) REFERENCES Food (id),
    FOREIGN KEY (macro_type_id) REFERENCES MacroType (id)
);

CREATE TABLE MacroType
(
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL
);

INSERT INTO MacroType (name)
VALUES ('Proteins');

INSERT INTO MacroType (name)
VALUES ('Fat');

INSERT INTO MacroType (name)
VALUES ('Carbs');
