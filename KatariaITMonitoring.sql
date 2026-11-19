/* ============================================================================
   Kataria IT Monitoring  -  DATABASE FIRST schema  (SQL Server 2016+)
   Run this whole script once in SSMS. It is safe to re-run (idempotent).
   Default login created:  username = adminpcc   password = admin@123
   ============================================================================ */
IF DB_ID(N'KatariaITMonitoring') IS NULL
    CREATE DATABASE KatariaITMonitoring;
GO
USE KatariaITMonitoring;
GO

/* ---------- Purani version ki tables ko "old" schema mein shift karna (data delete nahi hota) ----------
   Purani app ki DailyReports / ServerEntries / EvidenceFiles ke columns alag the.
   Jo table naye column (ReportId / ServerEntryId / EvidenceId) ke baghair ho, wo old schema mein chali jati hai. */
IF SCHEMA_ID(N'old') IS NULL EXEC(N'CREATE SCHEMA old');
GO
IF OBJECT_ID(N'dbo.EvidenceFiles') IS NOT NULL AND COL_LENGTH(N'dbo.EvidenceFiles', N'EvidenceId') IS NULL
BEGIN
    IF OBJECT_ID(N'old.EvidenceFiles') IS NOT NULL DROP TABLE old.EvidenceFiles;
    EXEC(N'ALTER SCHEMA old TRANSFER dbo.EvidenceFiles');
END
IF OBJECT_ID(N'dbo.ServerEntries') IS NOT NULL AND COL_LENGTH(N'dbo.ServerEntries', N'ServerEntryId') IS NULL
BEGIN
    IF OBJECT_ID(N'old.ServerEntries') IS NOT NULL DROP TABLE old.ServerEntries;
    EXEC(N'ALTER SCHEMA old TRANSFER dbo.ServerEntries');
END
IF OBJECT_ID(N'dbo.DailyReports') IS NOT NULL AND COL_LENGTH(N'dbo.DailyReports', N'ReportId') IS NULL
BEGIN
    IF OBJECT_ID(N'old.DailyReports') IS NOT NULL DROP TABLE old.DailyReports;
    EXEC(N'ALTER SCHEMA old TRANSFER dbo.DailyReports');
END
GO

/* ---------- Users (own login table, NOT ASP.NET Identity) ------------------ */
IF OBJECT_ID(N'dbo.AppUsers') IS NULL
CREATE TABLE dbo.AppUsers (
    UserId           INT IDENTITY(1,1) CONSTRAINT PK_AppUsers PRIMARY KEY,
    Username         NVARCHAR(50)  NOT NULL,
    DisplayName      NVARCHAR(100) NOT NULL,
    PasswordHash     NVARCHAR(300) NOT NULL,
    Role             NVARCHAR(20)  NOT NULL CONSTRAINT DF_AppUsers_Role DEFAULT N'Staff',
    IsActive         BIT           NOT NULL CONSTRAINT DF_AppUsers_IsActive DEFAULT 1,
    FailedLoginCount INT           NOT NULL CONSTRAINT DF_AppUsers_Failed DEFAULT 0,
    LockoutEndUtc    DATETIME2(0)  NULL,
    LastLoginUtc     DATETIME2(0)  NULL,
    CreatedAtUtc     DATETIME2(0)  NOT NULL CONSTRAINT DF_AppUsers_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_AppUsers_Username UNIQUE (Username),
    CONSTRAINT CK_AppUsers_Role CHECK (Role IN (N'Admin', N'Staff'))
);
GO

/* ---------- Master data ---------------------------------------------------- */
IF OBJECT_ID(N'dbo.Servers') IS NULL
CREATE TABLE dbo.Servers (
    ServerId   INT IDENTITY(1,1) CONSTRAINT PK_Servers PRIMARY KEY,
    Code       VARCHAR(10)   NOT NULL,
    Name       NVARCHAR(100) NOT NULL,
    IpAddress  VARCHAR(45)   NOT NULL,
    Location   NVARCHAR(100) NOT NULL,
    Role       NVARCHAR(250) NOT NULL,
    SortOrder  INT           NOT NULL CONSTRAINT DF_Servers_Sort DEFAULT 0,
    IsActive   BIT           NOT NULL CONSTRAINT DF_Servers_Active DEFAULT 1,
    CONSTRAINT UQ_Servers_Code UNIQUE (Code)
);
GO
IF OBJECT_ID(N'dbo.CheckItems') IS NULL
CREATE TABLE dbo.CheckItems (
    CheckItemId INT IDENTITY(1,1) CONSTRAINT PK_CheckItems PRIMARY KEY,
    Code        VARCHAR(20)   NOT NULL,
    Label       NVARCHAR(100) NOT NULL,
    SortOrder   INT           NOT NULL CONSTRAINT DF_CheckItems_Sort DEFAULT 0,
    IsActive    BIT           NOT NULL CONSTRAINT DF_CheckItems_Active DEFAULT 1,
    CONSTRAINT UQ_CheckItems_Code UNIQUE (Code)
);
GO

/* ---------- Daily report --------------------------------------------------- */
IF OBJECT_ID(N'dbo.DailyReports') IS NULL
CREATE TABLE dbo.DailyReports (
    ReportId            INT IDENTITY(1,1) CONSTRAINT PK_DailyReports PRIMARY KEY,
    ReportDate          DATE          NOT NULL,
    Shift               NVARCHAR(30)  NOT NULL CONSTRAINT DF_DR_Shift DEFAULT N'Daily inspection',
    PreparedByUserId    INT           NULL CONSTRAINT FK_DR_User REFERENCES dbo.AppUsers(UserId) ON DELETE SET NULL,
    PreparedByName      NVARCHAR(100) NOT NULL,
    ReviewedBy          NVARCHAR(100) NULL,
    RoomCondition       NVARCHAR(30)  NOT NULL CONSTRAINT DF_DR_Room DEFAULT N'Not Checked',
    ManagerNotification NVARCHAR(30)  NOT NULL CONSTRAINT DF_DR_Mgr DEFAULT N'No',
    InternetStatus      NVARCHAR(20)  NOT NULL CONSTRAINT DF_DR_Net DEFAULT N'Not Checked',
    InternetNotes       NVARCHAR(1000) NULL,
    UpsStatus           NVARCHAR(20)  NOT NULL CONSTRAINT DF_DR_Ups DEFAULT N'Not Checked',
    UpsNotes            NVARCHAR(1000) NULL,
    Summary             NVARCHAR(2000) NULL,
    OverallStatus       NVARCHAR(30)  NOT NULL CONSTRAINT DF_DR_Overall DEFAULT N'OK',
    -- Google Drive sync result
    DriveSyncStatus     NVARCHAR(20)  NOT NULL CONSTRAINT DF_DR_Drive DEFAULT N'Pending',  -- Pending / Uploaded / Partial / Failed / NotConfigured
    DriveSyncMessage    NVARCHAR(1000) NULL,
    DriveFolderId       NVARCHAR(100) NULL,
    DriveFolderLink     NVARCHAR(300) NULL,
    DriveExcelFileId    NVARCHAR(100) NULL,
    DriveExcelLink      NVARCHAR(300) NULL,
    DriveSyncedAtUtc    DATETIME2(0)  NULL,
    CreatedAtUtc        DATETIME2(0)  NOT NULL CONSTRAINT DF_DR_Created DEFAULT SYSUTCDATETIME()
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DailyReports_ReportDate' AND object_id = OBJECT_ID(N'dbo.DailyReports'))
    CREATE INDEX IX_DailyReports_ReportDate ON dbo.DailyReports(ReportDate DESC);
GO

IF OBJECT_ID(N'dbo.ServerEntries') IS NULL
CREATE TABLE dbo.ServerEntries (
    ServerEntryId INT IDENTITY(1,1) CONSTRAINT PK_ServerEntries PRIMARY KEY,
    ReportId      INT NOT NULL CONSTRAINT FK_SE_Report REFERENCES dbo.DailyReports(ReportId) ON DELETE CASCADE,
    ServerId      INT NOT NULL CONSTRAINT FK_SE_Server REFERENCES dbo.Servers(ServerId),
    Notes         NVARCHAR(1000) NULL,
    CONSTRAINT UQ_SE_Report_Server UNIQUE (ReportId, ServerId)
);
GO
IF OBJECT_ID(N'dbo.ServerCheckResults') IS NULL
CREATE TABLE dbo.ServerCheckResults (
    ServerEntryId INT NOT NULL CONSTRAINT FK_SCR_Entry REFERENCES dbo.ServerEntries(ServerEntryId) ON DELETE CASCADE,
    CheckItemId   INT NOT NULL CONSTRAINT FK_SCR_Item  REFERENCES dbo.CheckItems(CheckItemId),
    Status        NVARCHAR(20) NOT NULL,   -- Not Checked / OK / Issue / N/A
    CONSTRAINT PK_ServerCheckResults PRIMARY KEY (ServerEntryId, CheckItemId),
    CONSTRAINT CK_SCR_Status CHECK (Status IN (N'Not Checked', N'OK', N'Issue', N'N/A'))
);
GO

/* ---------- Photos (server / internet / ups) ------------------------------- */
IF OBJECT_ID(N'dbo.EvidenceFiles') IS NULL
CREATE TABLE dbo.EvidenceFiles (
    EvidenceId    INT IDENTITY(1,1) CONSTRAINT PK_EvidenceFiles PRIMARY KEY,
    ReportId      INT NOT NULL CONSTRAINT FK_EF_Report REFERENCES dbo.DailyReports(ReportId) ON DELETE CASCADE,
    ServerId      INT NULL CONSTRAINT FK_EF_Server REFERENCES dbo.Servers(ServerId),
    Category      VARCHAR(10)   NOT NULL,  -- Server / Internet / Ups
    OriginalName  NVARCHAR(260) NOT NULL,
    StoredName    NVARCHAR(100) NOT NULL,
    ContentType   NVARCHAR(100) NOT NULL,
    SizeBytes     BIGINT        NOT NULL,
    DriveFileId   NVARCHAR(100) NULL,
    DriveLink     NVARCHAR(300) NULL,
    DriveStatus   NVARCHAR(20)  NOT NULL CONSTRAINT DF_EF_Drive DEFAULT N'Pending',
    UploadedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_EF_Up DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_EF_Category CHECK (Category IN ('Server','Internet','Ups'))
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EvidenceFiles_Report' AND object_id = OBJECT_ID(N'dbo.EvidenceFiles'))
    CREATE INDEX IX_EvidenceFiles_Report ON dbo.EvidenceFiles(ReportId);
GO

/* ---------- Key/value settings (Google Drive refresh token etc.) ----------- */
IF OBJECT_ID(N'dbo.AppSettings') IS NULL
CREATE TABLE dbo.AppSettings (
    SettingKey   NVARCHAR(100) NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
    SettingValue NVARCHAR(MAX) NULL,
    UpdatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_AS_Upd DEFAULT SYSUTCDATETIME()
);
GO

/* ============================ SEED DATA ==================================== */
MERGE dbo.Servers AS t
USING (VALUES
 ('S1',N'Server 1','172.16.7.18',N'PCC IRAN ROAD',N'Workstation Server – Pharmacy WellnessphCRS',1),
 ('S2',N'Server 2','172.16.7.21',N'PCC IRAN ROAD',N'Dell EMC Rack Server – Pharmacy WellnessphWH',2),
 ('S3',N'Server 3','172.16.7.5', N'PCC IRAN ROAD',N'Dell EMC – ERP / VMS / replication of Server 4',3),
 ('S4',N'Server 4','172.16.7.8', N'PCC IRAN ROAD',N'Dell Server – Networking / VMS / replication of Server 3',4),
 ('S5',N'Server 5','192.168.16.4',N'PCC IRAN ROAD',N'AI Cloud Server – Head Office / Punjab server and Shakeel Sahab team',5)
) AS s(Code,Name,IpAddress,Location,Role,SortOrder) ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code,Name,IpAddress,Location,Role,SortOrder) VALUES (s.Code,s.Name,s.IpAddress,s.Location,s.Role,s.SortOrder);

MERGE dbo.CheckItems AS t
USING (VALUES
 ('power',   N'Power / electricity',    1),
 ('temp',    N'Temperature / heating',  2),
 ('network', N'Internet / network',     3),
 ('hardware',N'Hardware health',        4),
 ('backup',  N'Backup status',          5),
 ('disk',    N'Disk / storage',         6),
 ('os',      N'OS / required services', 7),
 ('overall', N'Overall status',         8)
) AS s(Code,Label,SortOrder) ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code,Label,SortOrder) VALUES (s.Code,s.Label,s.SortOrder);

-- Default admin: adminpcc / admin@123   (PBKDF2-SHA256, 600000 iterations)
-- CHANGE THIS PASSWORD from the Users page after first login.
IF NOT EXISTS (SELECT 1 FROM dbo.AppUsers WHERE Username = N'adminpcc')
INSERT dbo.AppUsers (Username, DisplayName, PasswordHash, Role)
VALUES (N'adminpcc', N'Administrator (PCC)',
        N'v1.600000.obLD1OX2BxgpOktcbX6PkA==.7prDvokx9BTF1r/cIVjoK+vZMGfjFSbj5Hq/ltWg06U=', N'Admin');
GO
