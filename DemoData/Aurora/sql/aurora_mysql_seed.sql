CREATE DATABASE IF NOT EXISTS aurora_demo;
USE aurora_demo;

DROP TABLE IF EXISTS aurora_cities;
DROP TABLE IF EXISTS aurora_risks;

CREATE TABLE aurora_cities (
    id INT PRIMARY KEY,
    name VARCHAR(80) NOT NULL,
    population INT NOT NULL,
    expected_savings_percent DECIMAL(5,2) NOT NULL,
    retail_uplift_percent DECIMAL(5,2) NOT NULL,
    grid_flex_mwh DECIMAL(6,2) NOT NULL,
    privacy_urgency INT NOT NULL,
    owner VARCHAR(120) NOT NULL
);

INSERT INTO aurora_cities VALUES
(1, 'Hamburg', 1892000, 10.50, 4.10, 8.70, 92, 'Lena Vogt'),
(2, 'Munich', 1512000, 8.80, 7.80, 6.40, 54, 'Omar Klein'),
(3, 'Cologne', 1088000, 9.40, 4.90, 14.20, 61, 'Priya Raman'),
(4, 'Berlin', 3878000, 7.10, 5.60, 10.10, 73, 'Mara Stein');

CREATE TABLE aurora_risks (
    id VARCHAR(20) PRIMARY KEY,
    area VARCHAR(80) NOT NULL,
    description TEXT NOT NULL,
    owner VARCHAR(120) NOT NULL,
    status VARCHAR(40) NOT NULL
);

INSERT INTO aurora_risks VALUES
('AUR-R1', 'Privacy', 'Hamburg sensor corridor may include school-zone movement patterns.', 'Lena Vogt', 'Open'),
('AUR-R2', 'Data Quality', 'Munich retail demand feed has weekend gaps.', 'Omar Klein', 'Monitoring'),
('AUR-R3', 'Infrastructure', 'Cologne energy telemetry arrives in 30-minute batches.', 'Priya Raman', 'In Progress'),
('AUR-R4', 'Governance', 'Berlin has overlapping approvals from transport, retail and citizen boards.', 'Mara Stein', 'Open');
