CREATE TABLE Food
(
    id     TEXT PRIMARY KEY,
    name   TEXT NOT NULL,
    brands TEXT,
    quantity REAL
);

CREATE TABLE Macros
(
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    food_id       TEXT    NOT NULL,
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
