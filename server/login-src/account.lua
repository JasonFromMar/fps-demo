local core = require "silly.core"
local crypt = require "crypt"
local log = require "log"
local db = require "db"
local const = require "const"
local login = require "logind"
local router = require "router"
local gaterpc = require "gaterpc"
local errno = require "protocol.errno"

local challenge_key = {}
local uid_online = {}

local function event_close(fd)
	challenge_key = {}
end

login.event_close(event_close)


local function r_create(fd, req)
	local uid = db.account_id(req.user)
	if not uid then
		uid = db.account_create(req.user, req.passwd)
	end
	uid = tonumber(uid)
	local ack = {
		uid = uid
	}
	login.send(fd, "a_create", ack)
	log.print("[account] create user:", req.user, "uid:", uid, "passwd", req.passwd)
end

local function r_challenge(fd, req)
	local key = crypt.randomkey()
	local ack = {
		randomkey = key
	}
	challenge_key[fd] = key
	login.send(fd, "a_challenge", ack)
	log.print("[account] challenge fd:", fd, key)
end


local rrpc_session = {
	uid = false
}

local rrpc_kick = {
	uid = false
}

local P = require "print"

local function auth(fd, user, passwd)
	local key = challenge_key[fd]
	if not key then
		return nil, errno.ACCOUNT_NO_CHALLENGE
	end
	challenge_key[fd] = nil
	local md5 , uid = db.account_passwd(user)
	if not md5 or not uid then
		return nil, errno.ACCOUNT_NO_USER
	end
	local hmac = crypt.hmac(md5 , key)
	if hmac ~= passwd then
		return nil, errno.ACCOUNT_NO_PASSWORD
	end
	uid = tonumber(uid)
	local kick_gate = uid_online[uid]
	if not kick_gate then
		return uid
	end
	local g = gaterpc.gate(kick_gate)
	assert(g, kick_gate)
	rrpc_kick.uid = uid
	local ack = g:call("rrpc_kick", rrpc_kick)
	if not ack then
		return nil, errno.ACCOUNT_KICK_TIMEOUT
	end
	return uid
end

local function r_login(fd, req)
	local uid, err = auth(fd, req.user, req.passwd)
	if not uid then
		return login.error(fd, "a_login", err)
	end
	local gateid = req.gateid
	local gate = gaterpc.gate(gateid)
	if not gate then
		return login.error(fd, "a_login", errno.ACCOUNT_NO_GATEID)
	end
	rrpc_session.uid = uid
	local ack = gate:call("rrpc_session", rrpc_session)
	if not ack then
		return login.error(fd, "a_login", errno.ACCOUNT_SESSION_TIMEOUT)
	end
	print("login gate session", uid, ack.session)
	local ack = {
		uid = uid,
		session = ack.session,
	}
	login.send(fd, "a_login", ack)
end

router.register("r_create", r_create)
router.register("r_challenge", r_challenge)
router.register("r_login", r_login)

