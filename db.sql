CREATE DATABASE FaceCheck;
GO

USE FaceCheck;
GO

CREATE TABLE Persons (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    Firstname VARCHAR(50) NOT NULL,
    Lastname VARCHAR(255) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    Status INT DEFAULT 10,
    CreatedAt DATETIME DEFAULT GETDATE()
);

CREATE TABLE Attendances (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    PersonId INT NOT NULL,
    CheckInTime DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PersonId) REFERENCES Persons(Id)
);

GO

-- Persons sample data
INSERT INTO Persons (Firstname, Lastname, Email)
VALUES 
('Alice', 'Smith', 'alice.smith@example.com'),
('Bob', 'Johnson', 'bob.johnson@example.com'),
('Charlie', 'Brown', 'charlie.brown@example.com'),
('Diana', 'Williams', 'diana.williams@example.com'),
('Ethan', 'Davis', 'ethan.davis@example.com');

GO


-- Attendances sample data
-- Day 1
INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(1, '2025-12-01 08:05:00'),
(2, '2025-12-01 08:10:00'),
(3, '2025-12-01 08:15:00'),
(4, '2025-12-01 08:08:00'),
(5, '2025-12-01 08:20:00');

-- Day 2
INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(1, '2025-12-02 08:03:00'),
(2, '2025-12-02 08:12:00'),
(3, '2025-12-02 08:18:00'),
(4, '2025-12-02 08:07:00'),
(5, '2025-12-02 08:22:00');

-- Day 3
INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(1, '2025-12-03 08:04:00'),
(2, '2025-12-03 08:09:00'),
(3, '2025-12-03 08:14:00'),
(4, '2025-12-03 08:06:00'),
(5, '2025-12-03 08:21:00');

-- Day 4
INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(1, '2025-12-04 08:05:00'),
(2, '2025-12-04 08:10:00'),
(3, '2025-12-04 08:15:00'),
(4, '2025-12-04 08:08:00'),
(5, '2025-12-04 08:20:00');

-- Day 5
INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(1, '2025-12-05 08:03:00'),
(2, '2025-12-05 08:11:00'),
(3, '2025-12-05 08:16:00'),
(4, '2025-12-05 08:07:00'),
(5, '2025-12-05 08:22:00');

GO

-- More persons sample data for pagination testing
INSERT INTO Persons (Firstname, Lastname, Email)
VALUES 
('Fiona', 'Miller', 'fiona.miller@example.com'),
('George', 'Wilson', 'george.wilson@example.com'),
('Hannah', 'Moore', 'hannah.moore@example.com'),
('Ian', 'Taylor', 'ian.taylor@example.com'),
('Julia', 'Anderson', 'julia.anderson@example.com');
GO

-- +--------------------------------------+
-- | MONTHLY ATTENDANCE REPORT PROCEDURE  |
-- +--------------------------------------+

CREATE FUNCTION GetMonthDays (@year INT, @month INT)
RETURNS TABLE
AS
RETURN
(
    SELECT 
        DATEFROMPARTS(@year, @month, n) AS DayDate,
        DATENAME(WEEKDAY, DATEFROMPARTS(@year, @month, n)) AS DayName,
        CASE 
            WHEN DATENAME(WEEKDAY, DATEFROMPARTS(@year, @month, n)) IN ('samedi', 'dimanche', 'Saturday', 'Sunday') 
                THEN 1 
            ELSE 0 
        END AS IsWeekend
    FROM (
        SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.objects
    ) AS numbers
    WHERE n <= DAY(EOMONTH(DATEFROMPARTS(@year, @month, 1)))
);

CREATE FUNCTION GetMonthlyAttendanceReport
(
    @year INT,
    @month INT
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        md.DayDate,
        md.DayName,
        md.IsWeekend,
        p.Id AS PersonId,
        p.Firstname,
        p.Lastname,
        CASE 
            WHEN a.PersonId IS NOT NULL THEN 1             -- Present
            WHEN p.CreatedAt > md.DayDate THEN -1          -- Not created yet
            ELSE 0                                         -- Absent
        END AS Attendance
    FROM
        GetMonthDays(@year, @month) AS md
    CROSS JOIN 
        Persons AS p
    LEFT JOIN 
        Attendances AS a
            ON a.PersonId = p.Id
            AND CAST(a.CheckInTime AS DATE) = md.DayDate
);

-- SAMPLE DATA TO TEST THE FUNCTION
INSERT INTO Persons (Firstname, Lastname, Email)
VALUES 
('To', 'MAMIARILAZA', 'mamiarilaza.to@gmail.com'),
('Tatiana', 'RAJAONASITERA', 'tatianarajao@gmail.com');

-- Day 5
INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(14, '2025-01-12 08:03:00'),
(14, '2025-02-12 08:11:00'),
(14, '2025-03-12 08:11:00'),
(14, '2025-08-12 08:11:00'),
(14, '2025-09-12 08:11:00'),
(14, '2025-10-12 08:11:00'),
(14, '2025-11-12 08:11:00'),
(14, '2025-12-12 08:22:00');

INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(13, '2025-05-12 08:03:00'),
(13, '2025-08-12 08:11:00'),
(13, '2025-09-12 08:11:00');

GO


UPDATE Persons SET CreatedAt = '2025-05-12' WHERE Id = 13;