-- =============================================================================
--  SYNTHETIC DEVELOPMENT DATA - NEVER RUN THIS ON A PRODUCTION DATABASE
-- =============================================================================
--  Medilog - F1 (appointments) local test data for MySQL.
--
--  * Every name, phone number and e-mail below is invented ("(DEV)" / example.test).
--    No real person or patient data is used (AGENTS.md section 4.3).
--  * password_hash holds a placeholder, NOT a real hash: these accounts cannot log in.
--    Authentication is not implemented yet (STATE.md Q-07); the API identifies the
--    caller from the temporary X-User-Id / X-User-Role headers (Development only).
--  * The script is idempotent: running it again changes nothing that already exists.
--    Use the cleanup section at the bottom to remove it.
--
--  How to run (after applying the EF migrations to the database):
--      mysql -u <user> -p <database> < backend/docs/dev/seed-dev-data.sql
--
--  Identities for the API headers  (X-User-Id / X-User-Role)
--  ---------------------------------------------------------------------------
--  Secretary (assigned to Doctor 1, Doctor 2 and the inactive Doctor 4):
--      aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa   Secretary
--  Doctor 1 (Cardiology):        11111111-1111-4111-8111-111111111111   Doctor
--  Doctor 2 (Dermatology):       22222222-2222-4222-8222-222222222222   Doctor
--  Doctor 3 (NOT assigned):      33333333-3333-4333-8333-333333333333   Doctor
--  Doctor 4 (INACTIVE, assigned): 44444444-4444-4444-8444-444444444444   Doctor
--
--  What it lets you exercise
--  ---------------------------------------------------------------------------
--  * Secretary sees Doctor 1 and Doctor 2 only (Doctor 3 is out of scope -> 404;
--    Doctor 4 is inactive -> not offered for booking).
--  * Patients belong to one doctor each; a patient of another doctor cannot be booked.
--  * A few appointments around today: Scheduled, Confirmed (back-to-back with the
--    first one: adjacency is allowed) and Cancelled (its slot is free again).
-- =============================================================================

SET NAMES utf8mb4;

-- -----------------------------------------------------------------------------
-- Doctors
-- -----------------------------------------------------------------------------
INSERT INTO doctors
    (id, full_name, email, password_hash, phone, specialization, account_status, created_at_utc)
VALUES
    ('11111111-1111-4111-8111-111111111111', 'Dr. Dev One (DEV)',   'dev.doctor1@example.test', 'DEV-ONLY-NOT-A-REAL-HASH', '0100000001', 'Cardiology',   'Active',   UTC_TIMESTAMP(6)),
    ('22222222-2222-4222-8222-222222222222', 'Dr. Dev Two (DEV)',   'dev.doctor2@example.test', 'DEV-ONLY-NOT-A-REAL-HASH', '0100000002', 'Dermatology', 'Active',   UTC_TIMESTAMP(6)),
    ('33333333-3333-4333-8333-333333333333', 'Dr. Dev Three (DEV)', 'dev.doctor3@example.test', 'DEV-ONLY-NOT-A-REAL-HASH', '0100000003', 'Pediatrics',  'Active',   UTC_TIMESTAMP(6)),
    ('44444444-4444-4444-8444-444444444444', 'Dr. Dev Four (DEV)',  'dev.doctor4@example.test', 'DEV-ONLY-NOT-A-REAL-HASH', '0100000004', 'Neurology',   'Inactive', UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE id = id;

-- -----------------------------------------------------------------------------
-- Secretary and her doctor assignments (doctor_secretaries)
-- -----------------------------------------------------------------------------
INSERT INTO secretaries
    (id, full_name, email, password_hash, phone, account_status, created_at_utc)
VALUES
    ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 'Secretary Dev (DEV)', 'dev.secretary@example.test', 'DEV-ONLY-NOT-A-REAL-HASH', '0100000099', 'Active', UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE id = id;

INSERT INTO doctor_secretaries (doctor_id, secretary_id)
VALUES
    ('11111111-1111-4111-8111-111111111111', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'),
    ('22222222-2222-4222-8222-222222222222', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'),
    ('44444444-4444-4444-8444-444444444444', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa')
ON DUPLICATE KEY UPDATE doctor_id = doctor_id;

-- -----------------------------------------------------------------------------
-- Patients (each belongs to exactly one doctor). Names are invented.
-- -----------------------------------------------------------------------------
INSERT INTO patients
    (id, doctor_id, full_name, date_of_birth, gender, phone, address, blood_type, emergency_contact, created_at_utc)
VALUES
    ('b0000001-0000-4000-8000-000000000001', '11111111-1111-4111-8111-111111111111', 'منى السيد (تجريبي)',     '1990-05-14', 'Female', '01012345601', NULL, NULL, NULL, UTC_TIMESTAMP(6)),
    ('b0000001-0000-4000-8000-000000000002', '11111111-1111-4111-8111-111111111111', 'Omar Hassan (DEV)',      '1985-11-02', 'Male',   '01012345602', NULL, NULL, NULL, UTC_TIMESTAMP(6)),
    ('b0000001-0000-4000-8000-000000000003', '11111111-1111-4111-8111-111111111111', 'Sara Ibrahim (DEV)',     '2001-03-27', 'Female', '01012345603', NULL, NULL, NULL, UTC_TIMESTAMP(6)),
    ('b0000002-0000-4000-8000-000000000001', '22222222-2222-4222-8222-222222222222', 'خالد مصطفى (تجريبي)',    '1978-08-19', 'Male',   '01112345601', NULL, NULL, NULL, UTC_TIMESTAMP(6)),
    ('b0000002-0000-4000-8000-000000000002', '22222222-2222-4222-8222-222222222222', 'Mariam Samir (DEV)',     '1995-01-09', 'Female', '01112345602', NULL, NULL, NULL, UTC_TIMESTAMP(6)),
    ('b0000003-0000-4000-8000-000000000001', '33333333-3333-4333-8333-333333333333', 'Youssef Adel (DEV)',     '2015-06-30', 'Male',   '01212345601', NULL, NULL, NULL, UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE id = id;

-- -----------------------------------------------------------------------------
-- Appointments around today (created by the secretary). Times are clinic-local.
-- -----------------------------------------------------------------------------
INSERT INTO appointments
    (id, patient_id, doctor_id, created_by, appointment_date, start_time, end_time, reason, status, notes, cancellation_reason, created_at_utc)
VALUES
    ('c0000001-0000-4000-8000-000000000001', 'b0000001-0000-4000-8000-000000000001', '11111111-1111-4111-8111-111111111111', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', DATE_ADD(CURDATE(), INTERVAL 1 DAY), '09:00:00', '09:30:00', 'Routine check (DEV)',     'Scheduled', NULL, NULL, UTC_TIMESTAMP(6)),
    ('c0000001-0000-4000-8000-000000000002', 'b0000001-0000-4000-8000-000000000002', '11111111-1111-4111-8111-111111111111', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', DATE_ADD(CURDATE(), INTERVAL 1 DAY), '09:30:00', '10:00:00', 'Follow-up (DEV)',         'Confirmed', NULL, NULL, UTC_TIMESTAMP(6)),
    ('c0000001-0000-4000-8000-000000000003', 'b0000001-0000-4000-8000-000000000003', '11111111-1111-4111-8111-111111111111', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', DATE_ADD(CURDATE(), INTERVAL 1 DAY), '11:00:00', '11:30:00', 'Consultation (DEV)',      'Cancelled', NULL, 'Patient asked to cancel (DEV)', UTC_TIMESTAMP(6)),
    ('c0000002-0000-4000-8000-000000000001', 'b0000002-0000-4000-8000-000000000001', '22222222-2222-4222-8222-222222222222', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', DATE_ADD(CURDATE(), INTERVAL 2 DAY), '10:00:00', '10:45:00', 'Skin check (DEV)',        'Scheduled', NULL, NULL, UTC_TIMESTAMP(6)),
    ('c0000003-0000-4000-8000-000000000001', 'b0000003-0000-4000-8000-000000000001', '33333333-3333-4333-8333-333333333333', '33333333-3333-4333-8333-333333333333', DATE_ADD(CURDATE(), INTERVAL 1 DAY), '09:00:00', '09:30:00', 'Out-of-scope for the secretary (DEV)', 'Scheduled', NULL, NULL, UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE id = id;

-- =============================================================================
--  CLEANUP (uncomment and run to remove ONLY the rows created above).
--  Order matters because of the foreign keys.
-- =============================================================================
-- DELETE FROM appointments       WHERE id LIKE 'c000000_-0000-4000-8000-00000000000_';
-- DELETE FROM doctor_secretaries WHERE secretary_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
-- DELETE FROM patients           WHERE id LIKE 'b000000_-0000-4000-8000-00000000000_';
-- DELETE FROM secretaries        WHERE id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
-- DELETE FROM doctors            WHERE id IN ('11111111-1111-4111-8111-111111111111',
--                                             '22222222-2222-4222-8222-222222222222',
--                                             '33333333-3333-4333-8333-333333333333',
--                                             '44444444-4444-4444-8444-444444444444');
