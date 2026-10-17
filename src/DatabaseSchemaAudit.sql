BEGIN;
SET LOCAL search_path = pickleverse, pg_catalog;
CREATE TEMP TABLE audit_checks (name text);
CREATE FUNCTION pg_temp.ok(condition boolean, label text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF condition IS DISTINCT FROM true THEN RAISE EXCEPTION 'FAIL: %',label; END IF;
    INSERT INTO pg_temp.audit_checks VALUES (label);
END; $$;
CREATE FUNCTION pg_temp.fails(command text, expected_state text, label text) RETURNS void LANGUAGE plpgsql AS $$
DECLARE actual_state text;
BEGIN
    BEGIN
        EXECUTE command;
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS actual_state = RETURNED_SQLSTATE;
        IF actual_state <> expected_state THEN RAISE EXCEPTION 'FAIL: %, expected %, got %: %',label,expected_state,actual_state,SQLERRM; END IF;
        INSERT INTO pg_temp.audit_checks VALUES (label);
        RETURN;
    END;
    RAISE EXCEPTION 'FAIL: % accepted invalid data',label;
END; $$;
DO $$
DECLARE
    u1 uuid; u2 uuid; u3 uuid; u4 uuid; credit uuid; purchase uuid; attempt uuid;
    grant_id uuid; daily uuid; weekly uuid; room_id_value uuid; match_value uuid; doubles_value uuid; bot_value uuid;
    feedback_value uuid; total_before bigint; today date := (now() AT TIME ZONE 'UTC')::date;
BEGIN
    PERFORM pg_temp.ok((SELECT count(*) = 12 FROM drills),'12 actual drills');
    PERFORM pg_temp.ok((SELECT count(*) = 27 FROM progress_definitions),'27 actual achievements and missions');
    PERFORM pg_temp.ok((SELECT count(*) = 10 FROM period_definitions),'lifetime achievement period');
    PERFORM pg_temp.ok((SELECT display_name = 'Người chơi' FROM roles WHERE role_code = 'player'),'UTF-8 Vietnamese seed');
    PERFORM pg_temp.ok((SELECT count(*) = 3 FROM item_prices),'actual cosmetic prices');
    INSERT INTO app_users(username,email) VALUES ('AuditOne','one@example.test') RETURNING user_id INTO u1;
    INSERT INTO app_users(username,email) VALUES ('AuditTwo','two@example.test') RETURNING user_id INTO u2;
    INSERT INTO app_users(username) VALUES ('AuditThree') RETURNING user_id INTO u3;
    INSERT INTO app_users(username) VALUES ('AuditFour') RETURNING user_id INTO u4;
    PERFORM pg_temp.ok((SELECT count(*) = 2 FROM player_wallets WHERE user_id = u1),'auto initialize two wallets');
    PERFORM pg_temp.ok((SELECT rating = 100 FROM player_rating_states WHERE user_id = u1),'Unity Elo starts at 100');
    PERFORM pg_temp.ok((SELECT item_code = 'paddle.default' FROM player_equipment WHERE user_id = u1),'default paddle is owned and equipped');
    PERFORM pg_temp.fails('INSERT INTO app_users(username) VALUES (''auditone'')','23505','case insensitive unique username');
    PERFORM pg_temp.fails('INSERT INTO app_users(username,email) VALUES (''OtherOne'',''ONE@example.test'')','23505','case insensitive unique email');
    PERFORM pg_temp.fails('INSERT INTO app_users(username,password_hash) VALUES (''UnsafePass'',''plaintext'')','23514','reject plaintext sized password');
    PERFORM pg_temp.fails(format('INSERT INTO user_roles(user_id,role_code) VALUES (%L,''player'')',u1),'23505','unique user role link');
    INSERT INTO reward_grants(user_id,grant_key,source_kind,xp_amount) VALUES (u1,'audit-credit','admin_adjustment',10) RETURNING reward_grant_id INTO credit;
    INSERT INTO wallet_transactions(user_id,currency_code,request_key,amount,balance_after,reason,reward_grant_id)
        VALUES (u1,'TOKENPAD','audit-credit-tokenpad',1000,0,'audit',credit);
    PERFORM pg_temp.ok((SELECT total_xp = 10 FROM player_progression WHERE user_id = u1),'reward XP applies once');
    PERFORM pg_temp.ok((SELECT balance = 1000 FROM player_wallets WHERE user_id = u1 AND currency_code = 'TOKENPAD'),'wallet credit applies');
    PERFORM pg_temp.ok((SELECT balance_after = 1000 FROM wallet_transactions WHERE reward_grant_id = credit),'wallet balance after is computed');
    SELECT purchase_cosmetic(u1,'paddle.thot','TOKENPAD','audit-purchase') INTO purchase;
    PERFORM pg_temp.ok((SELECT balance = 100 FROM player_wallets WHERE user_id = u1 AND currency_code = 'TOKENPAD'),'purchase atomically debits 900');
    PERFORM pg_temp.ok(EXISTS(SELECT 1 FROM player_inventory WHERE user_id=u1 AND item_code='paddle.thot'),'purchase grants ownership');
    PERFORM pg_temp.ok(purchase_cosmetic(u1,'paddle.thot','TOKENPAD','audit-purchase') = purchase,'same request returns original purchase');
    PERFORM pg_temp.ok((SELECT count(*) = 1 FROM shop_purchases WHERE user_id=u1),'retry never creates another purchase');
    PERFORM pg_temp.fails(format('SELECT purchase_cosmetic(%L,''paddle.thot'',''ELITE_TOKEN'',''audit-purchase'')',u1),'22023','reject reused request with different currency');
    PERFORM pg_temp.fails(format('SELECT purchase_cosmetic(%L,NULL,''TOKENPAD'',''audit-purchase'')',u1),'22023','reject reused request with null item');
    PERFORM pg_temp.fails(format('SELECT purchase_cosmetic(%L,''paddle.gemstone'',''ELITE_TOKEN'',''audit-short'')',u1),'23514','insufficient funds rolls back');
    PERFORM pg_temp.fails(format('SELECT purchase_cosmetic(%L,''paddle.default'',''TOKENPAD'',''audit-default'')',u1),'23514','default paddle cannot be purchased');
    PERFORM pg_temp.fails(format('SELECT purchase_cosmetic(%L,''paddle.thot'',''TOKENPAD'',''audit-second'')',u1),'23505','cannot buy an already owned cosmetic');
    PERFORM pg_temp.fails(format('UPDATE player_equipment SET item_code=''paddle.gemstone'' WHERE user_id=%L',u2),'23503','cannot equip unowned item');
    PERFORM pg_temp.fails(format('INSERT INTO player_equipment VALUES (%L,''avatar'',''paddle.default'',now())',u2),'23503','cannot equip paddle in avatar slot');
    PERFORM pg_temp.fails(format('INSERT INTO wallet_transactions(user_id,currency_code,request_key,amount,balance_after,reason) VALUES (%L,''TOKENPAD'',''audit-credit-tokenpad'',1000,0,''duplicate'') ON CONFLICT DO NOTHING',u1),'23505','duplicate wallet insert cannot credit through ON CONFLICT');
    PERFORM pg_temp.ok((SELECT balance=100 FROM player_wallets WHERE user_id=u1 AND currency_code='TOKENPAD'),'wallet remains unchanged after failed operations');
    PERFORM pg_temp.fails(format('UPDATE wallet_transactions SET amount=1 WHERE user_id=%L',u1),'55000','wallet ledger is immutable');
    PERFORM pg_temp.fails(format('DELETE FROM reward_grants WHERE reward_grant_id=%L',credit),'55000','reward ledger is immutable');
    PERFORM pg_temp.fails('INSERT INTO drill_prerequisites VALUES (''drive'',''modifiers'',1)','23514','drill prerequisites reject cycle');
    INSERT INTO training_attempts(user_id,client_attempt_id,drill_code,drill_version,status,total_trials,required_successes,
        completed_trials,successes,contacts,timing_total_seconds,ended_at)
    VALUES (u1,'audit-serve','serve',1,'completed',8,6,8,8,8,.64,now()) RETURNING attempt_id INTO attempt;
    PERFORM pg_temp.ok((SELECT stars=3 AND accuracy=1 AND average_timing_seconds=.08 FROM training_attempts WHERE attempt_id=attempt),'three star scoring matches Unity');
    INSERT INTO reward_grants(user_id,grant_key,source_kind,xp_amount) VALUES (u1,'audit-drill','drill',5) RETURNING reward_grant_id INTO grant_id;
    INSERT INTO drill_reward_claims VALUES (u1,'serve',attempt,grant_id,now());
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    PERFORM pg_temp.ok((SELECT total_xp=15 FROM player_progression WHERE user_id=u1),'drill reward adds XP');
    PERFORM pg_temp.ok((SELECT player_level=2 AND level_xp=5 AND next_level_xp=20 FROM v_player_levels WHERE user_id=u1),'level formula matches current game');
    PERFORM pg_temp.fails(format('INSERT INTO drill_reward_claims VALUES (%L,''serve'',%L,%L,now())',u1,attempt,grant_id),'23505','first drill completion reward cannot repeat');
    INSERT INTO training_attempts(user_id,client_attempt_id,drill_code,drill_version,total_trials,required_successes)
        VALUES (u1,'audit-pending','drive',1,8,6);
    PERFORM pg_temp.fails(format('INSERT INTO training_attempts(user_id,client_attempt_id,drill_code,drill_version,total_trials,required_successes) VALUES (%L,''audit-pending2'',''dink'',1,8,6)',u1),'23505','only one unfinished attempt per user');
    PERFORM pg_temp.fails(format('INSERT INTO reward_grants(user_id,grant_key,source_kind,xp_amount) VALUES (%L,''orphan-drill'',''drill'',5); SET CONSTRAINTS ALL IMMEDIATE',u1),'23514','orphan reward cannot commit');
    PERFORM pg_temp.ok((SELECT total_xp=15 FROM player_progression WHERE user_id=u1),'invalid grant rolls back XP');
    daily:=open_progress_period('daily',today); weekly:=open_progress_period('weekly',today);
    PERFORM pg_temp.ok((SELECT count(*)=7 FROM period_definitions WHERE period_id=daily),'daily period snapshots seven missions');
    PERFORM pg_temp.ok((SELECT count(*)=10 FROM period_definitions WHERE period_id=weekly),'weekly period snapshots ten missions');
    PERFORM pg_temp.ok(open_progress_period('daily',today)=daily,'period creation is idempotent');
    PERFORM pg_temp.fails(format('INSERT INTO period_definitions VALUES (%L,''W.Play10'',''daily'',10,20)',daily),'23503','daily period cannot contain weekly definition');
    PERFORM pg_temp.fails(format('INSERT INTO player_definition_progress(user_id,period_id,definition_code,progress_value,completed_at) VALUES (%L,%L,''D.Play1'',0,now())',u1,daily),'23514','cannot complete mission below target');
    INSERT INTO reward_grants(user_id,grant_key,source_kind,xp_amount) VALUES (u1,'daily1','daily_mission',5) RETURNING reward_grant_id INTO grant_id;
    INSERT INTO player_definition_progress(user_id,period_id,definition_code,progress_value,completed_at,claimed_at,reward_grant_id)
        VALUES (u1,daily,'D.Play1',1,now(),now(),grant_id);
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    PERFORM pg_temp.fails(format('INSERT INTO player_period_bonus_claims VALUES (%L,%L,%L,now())',u1,daily,credit),'23514','daily bonus requires all mission claims');
    INSERT INTO rooms(room_code,host_user_id,play_mode,room_kind) VALUES ('AUDITROOM',u1,'singles','custom') RETURNING room_id INTO room_id_value;
    INSERT INTO room_members(room_id,user_id,team_number,team_seat) VALUES (room_id_value,u1,0,1),(room_id_value,u2,1,1);
    PERFORM pg_temp.fails(format('INSERT INTO room_members(room_id,user_id) VALUES (%L,%L)',room_id_value,u3),'23514','singles room rejects third active human');
    PERFORM pg_temp.fails(format('UPDATE room_members SET team_seat=2 WHERE room_id=%L AND user_id=%L',room_id_value,u1),'23514','singles room rejects seat two');
    INSERT INTO friendships(user_low_id,user_high_id,requested_by) VALUES (least(u1,u2),greatest(u1,u2),u1);
    PERFORM pg_temp.fails(format('INSERT INTO friendships(user_low_id,user_high_id,requested_by) VALUES (%L,%L,%L)',greatest(u1,u2),least(u1,u2),u2),'23514','friendship requires canonical pair order');
    PERFORM pg_temp.fails(format('INSERT INTO player_blocks VALUES (%L,%L,now())',u1,u1),'23514','user cannot block self');
    INSERT INTO matches(match_kind,play_mode) VALUES ('ranked_singles','singles') RETURNING match_id INTO match_value;
    INSERT INTO match_teams VALUES (match_value,0,'A'),(match_value,1,'B');
    INSERT INTO match_participants(match_id,team_number,team_seat,user_id,actor_external_id,display_name_snapshot,outcome)
        VALUES (match_value,0,1,u1,'u1','AuditOne','won'),(match_value,1,1,u2,'u2','AuditTwo','lost');
    UPDATE matches SET status='completed',started_at=now(),ended_at=now(),winner_team_number=0 WHERE match_id=match_value;
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    PERFORM pg_temp.ok((SELECT count(*)=1 FROM v_player_match_history WHERE user_id=u1),'match history includes completed match');
    PERFORM pg_temp.fails(format('UPDATE match_participants SET outcome=''lost'' WHERE match_id=%L AND user_id=%L; SET CONSTRAINTS ALL IMMEDIATE',match_value,u1),'23514','match outcomes must agree with winning team');
    INSERT INTO rating_events(user_id,rating_system,play_mode,match_id,event_key,rating_before,rating_after,reason,algorithm_version)
        VALUES (u1,'elo','singles',match_value,'audit-match',100,120,'match','unity-elo-v1');
    PERFORM pg_temp.ok((SELECT rating=120 AND rated_matches=1 FROM player_rating_states WHERE user_id=u1),'rating event applies atomically');
    PERFORM pg_temp.fails(format('INSERT INTO rating_events(user_id,rating_system,play_mode,event_key,rating_before,rating_after,reason,algorithm_version) VALUES (%L,''elo'',''singles'',''stale'',100,140,''audit'',''v1'')',u1),'40001','stale rating result is rejected');
    PERFORM pg_temp.fails(format('INSERT INTO rating_events(user_id,rating_system,play_mode,match_id,event_key,rating_before,rating_after,reason,algorithm_version) VALUES (%L,''elo'',''singles'',%L,''second'',120,140,''audit'',''v1'')',u1,match_value),'23505','one rating event per player and match');
    PERFORM pg_temp.ok((SELECT position=1 FROM v_elo_leaderboard WHERE user_id=u1),'leaderboard ranks actual user rating');
    SELECT bot_profile_id INTO bot_value FROM bot_profiles WHERE bot_code='bot.easy';
    PERFORM pg_temp.fails(format('INSERT INTO match_participants(match_id,team_number,team_seat,user_id,bot_profile_id,actor_external_id,display_name_snapshot) VALUES (%L,0,2,%L,%L,''invalid'',''invalid'')',match_value,u3,bot_value),'23514','participant cannot be both bot and user');
    INSERT INTO matches(match_kind,play_mode) VALUES ('matched_doubles','doubles') RETURNING match_id INTO doubles_value;
    INSERT INTO match_teams VALUES (doubles_value,0,'A'),(doubles_value,1,'B');
    INSERT INTO match_participants(match_id,team_number,team_seat,user_id,actor_external_id,display_name_snapshot)
        VALUES (doubles_value,0,1,u1,'u1','One'),(doubles_value,1,1,u2,'u2','Two');
    PERFORM pg_temp.fails(format('UPDATE matches SET status=''running'',started_at=now() WHERE match_id=%L; SET CONSTRAINTS ALL IMMEDIATE',doubles_value),'23514','2v2 cannot start with only two humans');
    INSERT INTO match_participants(match_id,team_number,team_seat,user_id,actor_external_id,display_name_snapshot)
        VALUES (doubles_value,0,2,u3,'u3','Three'),(doubles_value,1,2,u4,'u4','Four');
    UPDATE matches SET status='running',started_at=now() WHERE match_id=doubles_value;
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    PERFORM pg_temp.ok((SELECT count(*)=4 FROM match_participants WHERE match_id=doubles_value),'valid four-player 2v2 starts');
    PERFORM pg_temp.fails(format('INSERT INTO coaching_feedback(user_id,match_id,summary,evaluator_kind,evaluator_version,processing_ms) VALUES (%L,%L,''invalid'',''rule'',''v1'',20)',u3,match_value),'23503','match feedback owner must participate');
    PERFORM pg_temp.fails(format('INSERT INTO coaching_feedback(user_id,attempt_id,summary,evaluator_kind,evaluator_version,processing_ms) VALUES (%L,%L,''invalid'',''rule'',''v1'',20)',u2,attempt),'23503','training feedback cannot target another player attempt');
    INSERT INTO coaching_feedback(user_id,attempt_id,summary,evaluator_kind,evaluator_version,processing_ms)
        VALUES (u1,attempt,'Good serve','rule','v1',20) RETURNING feedback_id INTO feedback_value;
    INSERT INTO feedback_errors VALUES (feedback_value,'late',1);
    PERFORM pg_temp.ok(EXISTS(SELECT 1 FROM feedback_errors WHERE feedback_id=feedback_value),'feedback has normalized error links');
    PERFORM pg_temp.fails(format('INSERT INTO match_replays(match_id,owner_user_id,storage_prefix) VALUES (%L,%L,''audit/'')',match_value,u3),'23503','replay owner must participate');
    PERFORM pg_temp.fails(format('INSERT INTO player_reports(reporter_user_id,reported_user_id,reason_code,description) VALUES (%L,%L,''other'',''self'')',u1,u1),'23514','reject self report');
    INSERT INTO player_daily_activity(user_id,activity_date,completed_matches) VALUES (u1,today-2,1),(u1,today-1,1),(u1,today,1);
    PERFORM pg_temp.ok((SELECT current_streak=3 AND best_streak=3 FROM v_player_daily_streaks WHERE user_id=u1),'UTC consecutive daily streak is derived correctly');
    INSERT INTO streak_reward_definitions VALUES ('audit3',3,0,100,true);
    INSERT INTO reward_grants(user_id,grant_key,source_kind) VALUES (u1,'audit-streak','daily_streak') RETURNING reward_grant_id INTO grant_id;
    INSERT INTO player_streak_reward_claims VALUES (u1,'audit3',today-2,today,grant_id,now());
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    PERFORM pg_temp.ok(EXISTS(SELECT 1 FROM player_streak_reward_claims WHERE user_id=u1),'valid streak milestone may be claimed');
    PERFORM pg_temp.fails(format('INSERT INTO player_streak_reward_claims VALUES (%L,''audit3'',%L,%L,%L,now())',u2,today-2,today,credit),'23514','streak requires actual consecutive activity');
END; $$;
SELECT count(*) AS passed_checks FROM pg_temp.audit_checks;
SET CONSTRAINTS ALL IMMEDIATE;
ROLLBACK;
