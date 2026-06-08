-- Add missing columns to rant_posts
ALTER TABLE rant_posts
ADD COLUMN IF NOT EXISTS hash_tags TEXT[] DEFAULT '{}',
ADD COLUMN IF NOT EXISTS image_data_url TEXT,
ADD COLUMN IF NOT EXISTS audio_data_url TEXT;

-- Add nested reply support + media to rant_replies
ALTER TABLE rant_replies
ADD COLUMN IF NOT EXISTS parent_reply_id VARCHAR(64) REFERENCES rant_replies(id) ON DELETE SET NULL,
ADD COLUMN IF NOT EXISTS image_data_url TEXT,
ADD COLUMN IF NOT EXISTS audio_data_url TEXT;

-- Index for fast sub-reply lookup
CREATE INDEX IF NOT EXISTS idx_rant_replies_parent ON rant_replies(parent_reply_id);
