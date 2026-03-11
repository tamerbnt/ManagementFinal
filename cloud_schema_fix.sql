-- Cloud Schema Fixes
-- Aligning Supabase tables with the app's updated model

-- 1. Ensure 'rfid_tag' exists instead of 'card_id'
DO $$ 
BEGIN
    -- For members table
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'members' AND column_name = 'card_id') THEN
        ALTER TABLE members RENAME COLUMN card_id TO rfid_tag;
    END IF;

    -- For staff_members table
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'staff_members' AND column_name = 'card_id') THEN
        ALTER TABLE staff_members RENAME COLUMN card_id TO rfid_tag;
    ELSIF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'staff_members' AND column_name = 'rfid_tag') THEN
        ALTER TABLE staff_members ADD COLUMN rfid_tag TEXT;
    END IF;

    -- 2. Ensure 'phone_number' exists in members
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'members' AND column_name = 'phone') THEN
        ALTER TABLE members RENAME COLUMN phone TO phone_number;
    ELSIF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'members' AND column_name = 'phone_number') THEN
        ALTER TABLE members ADD COLUMN phone_number TEXT;
    END IF;
END $$;
