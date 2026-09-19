# NEXORA AI API — Complete Integration Guide

> Yeh guide tujhe bataati hai ki **kisi bhi NEXORA app andar** AI kaise daalein
> (chat, summary, assistant — kuch bhi). Bas 2 steps: pehle login karke token lo,
> phir `/api/ai` call karo. **NVIDIA key kabhi app ke paas nahi jaati** — sab server
> side secure hai.

---

## Table of Contents

1. [Quick Overview](#1-quick-overview)
2. [How Auth Works (Token kaise milega)](#2-how-auth-works)
3. [Step 1 — Login & Get Token](#3-step-1--login-and-get-token)
4. [Step 2 — Call the AI Endpoint](#4-step-2--call-the-ai-endpoint)
5. [Request Body (Full Reference)](#5-request-body-full-reference)
6. [Response (JSON mode)](#6-response-json-mode)
7. [Streaming Mode (SSE)](#7-streaming-mode-sse)
8. [Error Codes & Handling](#8-error-codes--handling)
9. [Rate Limits & Caps](#9-rate-limits--caps)
10. [Security Notes](#10-security-notes)
11. [Code Examples](#11-code-examples)
    - [cURL (test)](#curl-test)
    - [Python](#python)
    - [Android / Kotlin (OkHttp)](#android--kotlin-okhttp)
    - [Android / Kotlin (Streaming)](#android--kotlin-streaming)
12. [Quick Start (final checklist)](#12-quick-start-final-checklist)

---

## 1. Quick Overview

| Item | Value |
|---|---|
| Base URL | `https://nexora-navy-omega.vercel.app` |
| AI Endpoint | `POST /api/ai` |
| Login Endpoint | `POST /api/auth-login` |
| Auth type | `Authorization: Bearer <token>` |
| Default model | `nvidia/nemotron-3-ultra-550b-a55b` |
| Response formats | `json` (default) or `stream` (SSE) |
| Token validity | 30 days (HMAC-signed, tamper-proof) |

**Flow:**
```
App  →  POST /api/auth-login  →  {token}
App  →  POST /api/ai  (Bearer token + prompts)  →  AI reply
```

---

## 2. How Auth Works

- **Admin** `auth.html` page pe user banata hai (username + password + optional expiry).
- **App** baseline login karta hai (section 3) → ek **token** milta hai.
- Yeh token **30 din valid** hota hai. App isse save karke **har AI call** pe
  `Authorization: Bearer <token>` bhejta hai.
- **Koi bhi bina token ke `/api/ai` call kare → `401`** (reject). Isliye tera endpoint
  secure hai — koi aur teri NVIDIA key use nahi kar sakta.

> Yaad rakho: App **password nahi** store karta — sirf token. Password sirf login ke liye bhejta hai.

---

## 3. Step 1 — Login & Get Token

### Request
```
POST https://nexora-navy-omega.vercel.app/api/auth-login
Content-Type: application/json
```

```json
{
  "username": "test",
  "password": "test123",
  "hwid": "unique_device_id"
}
```

- `hwid` — device ka fixed unique id (ANDROID_ID / saved UUID / fingerprint).
  Hamesha bhejo. (Agar HWID lock OFF hai toh ignore hota hai.)

### Success (200)
```json
{
  "ok": true,
  "token": "eyJzdWIiOiJ0ZXN0IiwiZXhwIjoxNzkxMDc2NDI3MDM5fQ.j3zrWpFMjwtp-Zti-phH7gCjBkdMWfklhYDuRdlWYVI",
  "user": {
    "username": "test",
    "hwid": "",
    "hwidAffect": false,
    "expiry": 1796260386784,
    "lastLogin": 1788484426849
  }
}
```

**App ko bas `token` chahiye.** Use securely store karo (Android: EncryptedSharedPreferences).

### Login errors
| HTTP | Meaning | App behavior |
|---|---|---|
| 400 | username/password missing or too long | validate + show msg |
| 401 | wrong credentials | "Wrong username or password" |
| 403 | account expired / HWID not allowed | block login |
| 429 | too many attempts | show countdown, wait |

---

## 4. Step 2 — Call the AI Endpoint

### Request
```
POST https://nexora-navy-omega.vercel.app/api/ai
Content-Type: application/json
Authorization: Bearer <TOKEN>
```

```json
{
  "model": "nvidia/nemotron-3-ultra-550b-a55b",
  "systemPrompt": "You are a smart travel assistant.",
  "userPrompt": "Suggest 3 days itinerary for Goa.",
  "thinking": true,
  "format": "json"
}
```

### Success (200) — JSON mode
```json
{
  "ok": true,
  "model": "nvidia/nemotron-3-ultra-550b-a55b",
  "thinking": true,
  "reasoning": "The user wants a compact...",
  "content": "Here is a 3-day itinerary...",
  "finishReason": "stop"
}
```

**Bas `content` ko app me dikhao.** Agar `thinking:false`, toh `reasoning` khali
(`""`) milega.

---

## 5. Request Body (Full Reference)

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `model` | string | no | `nvidia/nemotron-3-ultra-550b-a55b` | NVIDIA NIM model id |
| `systemPrompt` | string | no | `""` | system-level instructions (max 4000 chars) |
| `userPrompt` | string | **yes** | — | main user input (max 20000 chars) |
| `thinking` | bool | no | `false` | Nemotron reasoning on/off |
| `temperature` | number | no | `1` | creativity 0–2 |
| `top_p` | number | no | `0.95` | nucleus sampling 0–1 |
| `max_tokens` | number | no | `16384` | max output tokens 1–16384 |
| `format` | string | no | `"json"` | `json` or `stream` |

### Key points
- **`userPrompt` is mandatory.** Without it → `400`.
- `thinking:true` → extra `reasoning` field with chain-of-thought. **Slice `max_tokens`** bigger so reasoning + answer both fit.
- `format:"stream"` → SSE real-time chunks (section 7).

---

## 6. Response (JSON mode)

```json
{
  "ok": true,
  "model": "nvidia/nemotron-3-ultra-550b-a55b",
  "thinking": false,
  "reasoning": "",
  "content": "HELLO NEXORA",
  "finishReason": "stop"
}
```

| Field | Type | Meaning |
|---|---|---|
| `ok` | bool | true = success |
| `model` | string | model used |
| `thinking` | bool | reasoning on/off (as sent) |
| `reasoning` | string | chain-of-thought (only when thinking=true) |
| `content` | string | **the actual AI answer — show this** |
| `finishReason` | string | `stop` / `length` (length = max_tokens hit) |

---

## 7. Streaming Mode (SSE)

`format: "stream"` dene pe endpoint **NVIDIA ke raw SSE chunks pass-through** karta hai.
Content-Type: `text/event-stream`.

```
data: {"choices":[{"delta":{"content":"1","role":"assistant"},...}]}
data: {"choices":[{"delta":{"content":", 2,",...}]}
data: {"choices":[{"delta":{"content":" 3.",...}]}
data: {"choices":[{"delta":{...}},"finish_reason":"stop"}]
data: [DONE]
```

- Har `data:` line ek JSON chunk hai.
- Chunk ka content: `JSON.parse(line).choices[0].delta.content` (null for non-content deltas).
- `finish_reason:"stop"` ya `data: [DONE]` = end.
- **Best for:** live chat / typing effect / long responses.

---

## 8. Error Codes & Handling

| HTTP | `error` (body) | Meaning | App behavior |
|---|---|---|---|
| 400 | `userPrompt is required` | missing prompt | show "enter a message" |
| 400 | `userPrompt too long (max 20000)` | too long | trim input |
| 400 | `systemPrompt too long (max 4000)` | too long | shorten system prompt |
| 400 | `Invalid JSON` | malformed body | fix payload |
| 401 | `Missing Authorization Bearer token` | no token | go to login |
| 401 | `Invalid or expired token` | bad token | re-login, get new token |
| 429 | `Too many requests — wait a minute` | rate limited | show wait msg |
| 502 | `NVIDIA error <status>` + `detail` | upstream/model issue | retry (NVIDIA may be overloaded) |
| 500 | `NVIDIA API key not configured on server` | server config issue | contact admin |

### Retry logic (recommended)
1. `429` → wait 60s, retry.
2. `502` → wait 3–5s, retry up to 3 times (NVIDIA temporary overload common on big models).
3. `401` → clear token, re-login, continue.

---

## 9. Rate Limits & Caps

| Item | Limit |
|---|---|
| AI calls | 30 / min per user + IP |
| Login attempts | 10 / 5 min per username + IP |
| `userPrompt` | max 20000 chars |
| `systemPrompt` | max 4000 chars |
| `max_tokens` | 1 – 16384 |
| `temperature` | 0 – 2 |
| `top_p` | 0 – 1 |

> 429 aaye toh retry-after header bhi milta hai. Countdown dikha sakte ho.

---

## 10. Security Notes

1. **NVIDIA key kabhi app/endpoint me nahi** — sirf server env var `NVIDIA_API_KEY`.
2. Koi teri app reverse-engineer karke URL nikal bhi le → **bina token kuch nahi kar sakta**.
3. Token 30 din me expire → leaked token automatically useless.
4. Password app me store **mat** karo — sirf token (`EncryptedSharedPreferences`).
5. Token/password kisi logs me **print mat** karo.
6. If strict device-lock chahiye → HWID ON rakho (auth.html pe).

---

## 11. Code Examples

### cURL (test)
```bash
# 1) Login -> token
curl -X POST https://nexora-navy-omega.vercel.app/api/auth-login \
  -H "Content-Type: application/json" \
  -d '{"username":"test","password":"test123","hwid":"dev123"}'
# -> copy token from response

# 2) AI JSON
curl -X POST https://nexora-navy-omega.vercel.app/api/ai \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{"userPrompt":"Hello!","thinking":true,"format":"json"}'

# 3) AI Stream
curl -N -X POST https://nexora-navy-omega.vercel.app/api/ai \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{"userPrompt":"Hello!","thinking":false,"format":"stream"}'
```

### Python
```python
import requests

BASE = "https://nexora-navy-omega.vercel.app"

# login
r = requests.post(f"{BASE}/api/auth-login",
    json={"username": "test", "password": "test123", "hwid": "dev123"})
token = r.json()["token"]

# ai (json)
r = requests.post(f"{BASE}/api/ai",
    headers={"Authorization": f"Bearer {token}"},
    json={
        "userPrompt": "Write a short poem about the sea.",
        "thinking": False,
        "format": "json",
    })
data = r.json()
print(data["content"])
```

### Android / Kotlin (OkHttp) — JSON
```kotlin
val client = OkHttpClient.Builder()
    .connectTimeout(20, TimeUnit.SECONDS)
    .readTimeout(120, TimeUnit.SECONDS)   // big model takes time
    .build()

fun loginAndAI(username: String, password: String, hwid: String, prompt: String, onText: (String?, String?) -> Unit) {
    // 1) login
    val loginBody = JSONObject()
        .put("username", username)
        .put("password", password)
        .put("hwid", hwid).toString()

    val loginReq = Request.Builder()
        .url("https://nexora-navy-omega.vercel.app/api/auth-login")
        .post(loginBody.toRequestBody("application/json".toMediaType()))
        .build()

    client.newCall(loginReq).enqueue(object : Callback {
        override fun onFailure(call: Call, e: IOException) { onText(null, "network") }
        override fun onResponse(call: Call, resp: Response) {
            resp.use {
                val json = it.body?.string()?.let { s -> runCatching { JSONObject(s) }.getOrNull() }
                val token = json?.optString("token").takeIf { it?.isNotBlank() == true }
                if (token == null) { onText(null, "login_failed"); return }
                // 2) ai
                val aiBody = JSONObject()
                    .put("userPrompt", prompt)
                    .put("thinking", true)
                    .put("format", "json").toString()
                val aiReq = Request.Builder()
                    .url("https://nexora-navy-omega.vercel.app/api/ai")
                    .header("Authorization", "Bearer $token")
                    .post(aiBody.toRequestBody("application/json".toMediaType()))
                    .build()
                client.newCall(aiReq).enqueue(object : Callback {
                    override fun onFailure(call: Call, e: IOException) { onText(null, "network") }
                    override fun onResponse(call: Call, r: Response) {
                        r.use {
                            val res = it.body?.string()?.let { s -> runCatching { JSONObject(s) }.getOrNull() }
                            onText(res?.optString("content"), if (it.isSuccessful) null else res?.optString("error"))
                        }
                    }
                })
            }
        }
    })
}
```

### Android / Kotlin (OkHttp) — Streaming (SSE)
```kotlin
// Iske liye OkHttp use karo. Source: https://publicobject.com/
fun streamAI(token: String, prompt: String, onChunk: (String) -> Unit, onDone: () -> Unit) {
    val body = JSONObject()
        .put("userPrompt", prompt)
        .put("thinking", false)
        .put("format", "stream").toString()

    val request = Request.Builder()
        .url("https://nexora-navy-omega.vercel.app/api/ai")
        .header("Authorization", "Bearer $token")
        .post(body.toRequestBody("application/json".toMediaType()))
        .build()

    client.newCall(request).execute().use { resp ->
        resp.source().use { src ->
            while (!src.exhausted()) {
                val line = src.readUtf8Line() ?: break
                if (!line.startsWith("data:")) continue
                val payload = line.removePrefix("data:").trim()
                if (payload == "[DONE]") { onDone(); break }
                val chunk = runCatching { JSONObject(payload) }.getOrNull()
                val text = chunk?.optJSONObject("choices")
                    ?.optJSONObject(0)?.optJSONObject("delta")?.optString("content")
                if (!text.isNullOrEmpty()) onChunk(text)
            }
        }
    }
}
```

---

## 12. Quick Start (final checklist)

1. **Admin** → `auth.html` → create user (`test` / `test123` in example).
2. **App** → `POST /api/auth-login` (username + password + hwid) → **save token**.
3. **App** → `POST /api/ai` (Bearer token + `userPrompt`) → show `content`.
4. Optional: set `thinking:true` for reasoning, `format:"stream"` for live typing.
5. Handle `401` (re-login), `429` (wait), `502` (retry).

> **Yeh guide puri hai.** AI/developer ko bas yeh file mili toh app me AI implement kar sakta hai — login se leke streaming tak.

For full user-management details see `NEXORA-AUTH.md`.
