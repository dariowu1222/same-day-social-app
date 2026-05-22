create table if not exists users (
  id varchar(64) primary key,
  nickname varchar(80) not null,
  age_range varchar(40),
  gender varchar(40),
  location_area varchar(80),
  bio varchar(500) not null default '',
  interest_tags text[] not null default array[]::text[],
  value_tags text[] not null default array[]::text[],
  response_preference varchar(40) not null default 'JUST_LISTEN',
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists auth_accounts (
  id varchar(64) primary key,
  user_id varchar(64) not null references users(id) on delete cascade,
  username varchar(120) not null,
  password_hash varchar(500) not null,
  last_login_at timestamptz,
  is_disabled boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
create unique index if not exists ux_auth_accounts_username on auth_accounts(username);
create index if not exists ix_auth_accounts_user_id on auth_accounts(user_id);

create table if not exists password_reset_tokens (
  id varchar(64) primary key,
  user_id varchar(64) not null references users(id) on delete cascade,
  code_hash varchar(500) not null,
  expires_at timestamptz not null,
  used_at timestamptz,
  created_at timestamptz not null default now()
);
create index if not exists ix_password_reset_tokens_user_expires on password_reset_tokens(user_id, expires_at);

create table if not exists today_entries (
  id varchar(64) primary key,
  user_id varchar(64) not null references users(id) on delete cascade,
  content varchar(2000) not null,
  event_type varchar(40) not null default 'RANDOM',
  emotion_tags text[] not null default array[]::text[],
  value_tags text[] not null default array[]::text[],
  interest_tags text[] not null default array[]::text[],
  response_mode varchar(40) not null default 'JUST_LISTEN',
  visibility varchar(40) not null default 'MATCH_ONLY',
  created_at timestamptz not null default now()
);
create index if not exists ix_today_entries_user_created on today_entries(user_id, created_at desc);
create index if not exists ix_today_entries_event_created on today_entries(event_type, created_at desc);

create table if not exists matches (
  id varchar(64) primary key,
  user_id varchar(64) not null references users(id) on delete cascade,
  matched_user_id varchar(64) not null references users(id) on delete cascade,
  match_score integer not null default 0,
  match_type varchar(40) not null default 'SAME_EMOTION',
  shared_tags text[] not null default array[]::text[],
  reason varchar(500) not null default '',
  icebreaker varchar(500) not null default '',
  user_liked boolean not null default false,
  matched_user_liked boolean not null default false,
  created_at timestamptz not null default now()
);
create unique index if not exists ux_matches_user_matched on matches(user_id, matched_user_id);
create index if not exists ix_matches_user_score on matches(user_id, match_score desc);

create table if not exists rant_posts (
  id varchar(64) primary key,
  user_id varchar(64) not null references users(id) on delete cascade,
  nickname varchar(80) not null,
  content varchar(3000) not null,
  mode varchar(40) not null default 'JUST_SAYING',
  emotion_tags text[] not null default array[]::text[],
  is_hidden boolean not null default false,
  report_count integer not null default 0,
  created_at timestamptz not null default now()
);
create index if not exists ix_rant_posts_hidden_created on rant_posts(is_hidden, created_at desc);

create table if not exists rant_replies (
  id varchar(64) primary key,
  rant_post_id varchar(64) not null references rant_posts(id) on delete cascade,
  user_id varchar(64) not null references users(id) on delete cascade,
  nickname varchar(80) not null,
  content varchar(1000) not null,
  created_at timestamptz not null default now()
);
create index if not exists ix_rant_replies_post_created on rant_replies(rant_post_id, created_at);

create table if not exists rant_reactions (
  id varchar(64) primary key,
  rant_post_id varchar(64) not null references rant_posts(id) on delete cascade,
  user_id varchar(64) not null references users(id) on delete cascade,
  reaction_type varchar(40) not null default 'UNDERSTAND',
  created_at timestamptz not null default now()
);
create unique index if not exists ux_rant_reactions_post_user_type on rant_reactions(rant_post_id, user_id, reaction_type);

create table if not exists rant_reports (
  id varchar(64) primary key,
  rant_post_id varchar(64) not null references rant_posts(id) on delete cascade,
  user_id varchar(64) not null references users(id) on delete cascade,
  reason varchar(500),
  created_at timestamptz not null default now()
);
create unique index if not exists ux_rant_reports_post_user on rant_reports(rant_post_id, user_id);

create table if not exists tasks (
  id varchar(64) primary key,
  title varchar(120) not null,
  description varchar(1000) not null,
  category varchar(40) not null,
  duration varchar(80) not null,
  difficulty varchar(40) not null default 'EASY',
  participant_limit integer not null default 2,
  created_at timestamptz not null default now()
);
create index if not exists ix_tasks_category on tasks(category);

create table if not exists task_participants (
  id varchar(64) primary key,
  task_id varchar(64) not null references tasks(id) on delete cascade,
  user_id varchar(64) not null references users(id) on delete cascade,
  joined_at timestamptz not null default now()
);
create unique index if not exists ux_task_participants_task_user on task_participants(task_id, user_id);

create table if not exists chat_rooms (
  id varchar(64) primary key,
  source_type varchar(40) not null default 'TODAY_MATCH',
  source_id varchar(64) not null,
  created_at timestamptz not null default now()
);
create index if not exists ix_chat_rooms_source on chat_rooms(source_type, source_id);

create table if not exists chat_room_users (
  id varchar(64) primary key,
  chat_room_id varchar(64) not null references chat_rooms(id) on delete cascade,
  user_id varchar(64) not null references users(id) on delete cascade,
  joined_at timestamptz not null default now()
);
create unique index if not exists ux_chat_room_users_room_user on chat_room_users(chat_room_id, user_id);
create index if not exists ix_chat_room_users_user on chat_room_users(user_id);

create table if not exists chat_messages (
  id varchar(64) primary key,
  chat_room_id varchar(64) not null references chat_rooms(id) on delete cascade,
  sender_id varchar(64) not null references users(id) on delete cascade,
  content varchar(2000) not null,
  created_at timestamptz not null default now()
);
create index if not exists ix_chat_messages_room_created on chat_messages(chat_room_id, created_at);
