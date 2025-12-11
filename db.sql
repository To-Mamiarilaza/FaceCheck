CREATE DATABASE FaceCheck;
GO

USE FaceCheck;
GO

CREATE TABLE Persons (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    Firstname VARCHAR(50) NOT NULL,
    Lastname VARCHAR(255) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
<<<<<<< HEAD
    Password VARCHAR(255) NOT NULL,
=======
>>>>>>> e952940 ([FEAT] : FaceID)
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
GO

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
GO

-- +----------------------------------+
-- | MONTHLY ABSCENCE RATE PROCEDURE  |
-- +----------------------------------+

CREATE FUNCTION GetMonthlyAbsenceRate
(
    @year INT,
    @month INT
)
RETURNS TABLE
AS
RETURN
(
    SELECT 
        CAST (SUM (
            CASE WHEN (IsWeekend = 0 and Attendance = 0 and DayDate < GETDATE()) THEN 1 ELSE 0 END
        ) AS INT) AS TotalAbsences,
        CAST (SUM (
            CASE WHEN (IsWeekend = 0 and Attendance != -1 and DayDate < GETDATE()) THEN 1 ELSE 0 END
        ) AS INT) AS TotalWorkingDays
    FROM
        GetMonthlyAttendanceReport(@year, @month)
);
GO

CREATE FUNCTION GetYearlyAbsenceRates
(
    @year INT
)
RETURNS TABLE
AS
RETURN 
(
    SELECT
        Months.Month,
        (
            SELECT 
                CASE 
                    WHEN TotalWorkingDays = 0 THEN 0
                    ELSE (TotalAbsences * 100) / CAST(TotalWorkingDays AS FLOAT) 
                END
            FROM GetMonthlyAbsenceRate(@year, Months.Month)
        ) as Rate
    FROM
    (
        SELECT TOP 12 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Month
        FROM sys.objects
    ) as Months
);
GO

-- SAMPLE DATA TO TEST THE FUNCTION
-- Password hashes generated with bcrypt (both use "password123" for testing)
-- To: mamiarilaza.to@gmail.com / password123
-- Tatiana: tatianarajao@gmail.com / password123

INSERT INTO Persons (Firstname, Lastname, Email, Password)
VALUES 
('To', 'MAMIARILAZA', 'mamiarilaza.to@gmail.com', '$2a$11$PLvABitWr1fLRbGI2EBrBuyowaopdTcNlmk.2orJ.P32VnPV.eOvm'),
('Tatiana', 'RAJAONASITERA', 'tatianarajao@gmail.com', '$2a$11$PLvABitWr1fLRbGI2EBrBuyowaopdTcNlmk.2orJ.P32VnPV.eOvm');
CREATE TABLE Picture_Directory ( Id INT IDENTITY(1,1) PRIMARY KEY, PersonId INT NOT NULL, Url VARCHAR(255) NOT NULL, CreatedAt DATETIME DEFAULT GETDATE(), FOREIGN KEY (PersonId) REFERENCES Persons(Id) );

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
(1, '2025-01-12 08:03:00'),
(1, '2025-02-12 08:11:00'),
(1, '2025-03-12 08:11:00'),
(1, '2025-08-12 08:11:00'),
(1, '2025-09-12 08:11:00'),
(1, '2025-10-12 08:11:00'),
(1, '2025-11-12 08:11:00'),
(1, '2025-12-12 08:22:00');

INSERT INTO Attendances (PersonId, CheckInTime)
VALUES
(2, '2025-05-12 08:03:00'),
(2, '2025-08-12 08:11:00'),
(2, '2025-09-12 08:11:00');

GO

UPDATE Persons SET CreatedAt = '2025-05-12' WHERE Id = 2;
UPDATE Persons SET CreatedAt = '2025-01-12' WHERE Id = 1;


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

INSERT INTO Picture_Directory (PersonId, Url, CreatedAt) VALUES (1, '/home/zaby/PHONE/DOWLOAD/PXL_20250805_111510309.NIGHT_3.png', GETDATE());

GO
