#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#include <windows.h>
#include <commctrl.h>
#include <shellapi.h>
#include <shlobj.h>
#include <uxtheme.h>
#include <wchar.h>
#include <stdio.h>
#include <stdarg.h>

#define MAX_MODS 1024
#define MAX_TEXT 512
#define MAX_BUF 1048576
#define MAX_DEPS 16
#define MAX_PROFILE 128
#define CURRENT_PROFILE_LABEL L"Current settings.save"

typedef struct ModInfo {
    WCHAR id[MAX_TEXT];
    WCHAR name[MAX_TEXT];
    WCHAR version[MAX_TEXT];
    WCHAR minGameVersion[MAX_TEXT];
    WCHAR source[32];
    WCHAR group[MAX_TEXT];
    WCHAR manifest[MAX_PATH * 2];
    WCHAR workshopId[MAX_TEXT];
    WCHAR deps[MAX_DEPS][MAX_TEXT];
    WCHAR depMinVersions[MAX_DEPS][MAX_TEXT];
    int depCount;
    WCHAR orderAfter[MAX_DEPS][MAX_TEXT];
    int orderAfterCount;
    WCHAR orderBefore[MAX_DEPS][MAX_TEXT];
    int orderBeforeCount;
    int depth;
    BOOL affectsGameplay;
} ModInfo;

static HINSTANCE g_instance;
static HWND g_list, g_status, g_gamePath, g_settingsPath, g_title, g_subtitle;
static HWND g_btnRefresh, g_btnVanilla, g_btnLaunch, g_btnUp, g_btnDown, g_btnSaveOrder;
static HWND g_orderEdit, g_btnApplyOrder, g_profileCombo;
static HFONT g_font, g_titleFont;
static HBRUSH g_bgBrush;
static COLORREF g_bgColor = RGB(246, 243, 235);
static COLORREF g_textColor = RGB(45, 38, 32);
static WCHAR g_appDir[MAX_PATH * 2], g_gameDir[MAX_PATH * 2], g_settingsFile[MAX_PATH * 2];
static WCHAR g_gameVersion[MAX_TEXT];
static WCHAR g_steamExe[MAX_PATH * 2], g_steamArgs[8192];
static DWORD g_waitForPid;
static BOOL g_chinese, g_diagnose, g_selfTestOrder, g_selfTestSettings;
static ModInfo g_mods[MAX_MODS];
static int g_modCount;
static BOOL g_enforcingChecks;
static WCHAR g_savedOrder[MAX_MODS][MAX_TEXT];
static int g_savedOrderCount;
static WCHAR g_savedEnabled[MAX_MODS][MAX_TEXT];
static int g_savedEnabledCount;
static BOOL g_refreshingProfiles;

static const WCHAR* T(const WCHAR* zh, const WCHAR* en) { (void)zh; return en; }
static char* ReplaceSpan(char* src, DWORD* size, DWORD start, DWORD oldLen, const char* newText);
static int GetSelectedListIndex(void);
static void RebuildListPreservingChecks(int selectedIndex);
static BOOL RepairDependencyOrderInPlace(void);
static void JsonDependencyIds(const char* json, WCHAR deps[MAX_DEPS][MAX_TEXT], int* depCount);
static void JsonDependencyVersionRequirements(const char* json, WCHAR deps[MAX_DEPS][MAX_TEXT], WCHAR minVersions[MAX_DEPS][MAX_TEXT], int* depCount);
static void JsonMinGameVersion(const char* json, WCHAR* out, int cap);
static void JsonLoadAfterIds(const char* json, WCHAR ids[MAX_DEPS][MAX_TEXT], int* idCount);
static void JsonLoadBeforeIds(const char* json, WCHAR ids[MAX_DEPS][MAX_TEXT], int* idCount);
static BOOL LoadOrderFromPath(const WCHAR* path);
static BOOL LoadOrderFromPathCore(const WCHAR* path, BOOL rebuildList);
static void RefreshList(void);
static void ApplyModGroups(void);
static void EnforceDependencyChecks(int changedIndex);
static int PruneInvalidChecks(void);
static BOOL DirectoryHasSidecarManifest(const WCHAR* path);
static void AppendLogf(const WCHAR* fmt, ...);
static BOOL FindModListSpan(const char* json, DWORD size, DWORD* start, DWORD* len);
static void SaveNamedProfile(void);

static void DirName(WCHAR* path) {
    WCHAR* slash = wcsrchr(path, L'\\');
    if (slash) *slash = 0;
}

static BOOL FileExistsW2(const WCHAR* path) {
    DWORD a = GetFileAttributesW(path);
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

static BOOL DirExistsW2(const WCHAR* path) {
    DWORD a = GetFileAttributesW(path);
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY);
}

static void JoinPath(WCHAR* out, size_t cap, const WCHAR* a, const WCHAR* b) {
    if (cap == 0) return;
    wcsncpy(out, a, cap - 1);
    out[cap - 1] = 0;
    size_t len = wcslen(out);
    if (len + 1 < cap && len > 0 && out[len - 1] != L'\\') {
        out[len++] = L'\\';
        out[len] = 0;
    }
    if (len < cap - 1) {
        wcsncat(out, b, cap - len - 1);
    }
}

static const char* SkipWs(const char* p) {
    while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n') ++p;
    return p;
}

static char* ReadFileBytes(const WCHAR* path, DWORD* outSize) {
    HANDLE h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return NULL;
    DWORD size = GetFileSize(h, NULL);
    if (size == INVALID_FILE_SIZE || size > MAX_BUF * 8) { CloseHandle(h); return NULL; }
    char* data = (char*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, size + 1);
    if (!data) { CloseHandle(h); return NULL; }
    DWORD read = 0;
    ReadFile(h, data, size, &read, NULL);
    CloseHandle(h);
    data[read] = 0;
    if (outSize) *outSize = read;
    return data;
}

static const char* JsonStart(const char* json) {
    const unsigned char* p = (const unsigned char*)json;
    if (p[0] == 0xEF && p[1] == 0xBB && p[2] == 0xBF) p += 3;
    return SkipWs((const char*)p);
}

static BOOL WriteFileBytes(const WCHAR* path, const char* data, DWORD size) {
    HANDLE h = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return FALSE;
    DWORD written = 0;
    BOOL ok = WriteFile(h, data, size, &written, NULL);
    CloseHandle(h);
    return ok && written == size;
}

static void Utf8ToWide2(const char* s, int len, WCHAR* out, int cap) {
    if (cap <= 0) return;
    int n = MultiByteToWideChar(CP_UTF8, 0, s, len, out, cap - 1);
    out[n < 0 ? 0 : n] = 0;
}

static int WideToUtf82(const WCHAR* s, char* out, int cap) {
    return WideCharToMultiByte(CP_UTF8, 0, s, -1, out, cap, NULL, NULL);
}

static BOOL JsonStringValue(const char* json, const char* key, WCHAR* out, int cap) {
    json = JsonStart(json);
    char pattern[128];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);
    const char* p = strstr(json, pattern);
    if (!p) return FALSE;
    p = strchr(p + strlen(pattern), ':');
    if (!p) return FALSE;
    p = SkipWs(p + 1);
    if (*p != '"') return FALSE;
    ++p;
    const char* start = p;
    while (*p && *p != '"') {
        if (*p == '\\' && p[1]) p += 2;
        else ++p;
    }
    Utf8ToWide2(start, (int)(p - start), out, cap);
    return TRUE;
}

static BOOL JsonBoolInObject(const char* objectStart, const char* objectEnd, const char* key, BOOL* value) {
    char pattern[64];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);
    const char* p = strstr(objectStart, pattern);
    if (!p || p >= objectEnd) return FALSE;
    const char* colon = strchr(p + strlen(pattern), ':');
    if (!colon || colon >= objectEnd) return FALSE;
    const char* v = SkipWs(colon + 1);
    if (v >= objectEnd) return FALSE;
    if (strncmp(v, "true", 4) == 0) {
        *value = TRUE;
        return TRUE;
    }
    if (strncmp(v, "false", 5) == 0) {
        *value = FALSE;
        return TRUE;
    }
    return FALSE;
}

static BOOL JsonDependencyObjectIsOptional(const char* objectStart, const char* objectEnd) {
    BOOL value = FALSE;
    if (JsonBoolInObject(objectStart, objectEnd, "optional", &value) && value) return TRUE;
    if (JsonBoolInObject(objectStart, objectEnd, "is_optional", &value) && value) return TRUE;
    if (JsonBoolInObject(objectStart, objectEnd, "isOptional", &value) && value) return TRUE;
    if (JsonBoolInObject(objectStart, objectEnd, "required", &value) && !value) return TRUE;
    return FALSE;
}

static BOOL JsonStringInObject(const char* objectStart, const char* objectEnd, const char* key, WCHAR* out, int cap) {
    if (!out || cap <= 0) return FALSE;
    out[0] = 0;
    char pattern[96];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);
    const char* p = strstr(objectStart, pattern);
    if (!p || p >= objectEnd) return FALSE;
    const char* colon = strchr(p + strlen(pattern), ':');
    if (!colon || colon >= objectEnd) return FALSE;
    const char* value = SkipWs(colon + 1);
    if (value >= objectEnd || *value != '"') return FALSE;
    ++value;
    const char* start = value;
    while (*value && value < objectEnd && *value != '"') {
        if (*value == '\\' && value[1]) value += 2;
        else ++value;
    }
    if (value <= start) return FALSE;
    Utf8ToWide2(start, (int)(value - start), out, cap);
    return out[0] != 0;
}

static BOOL JsonDependencyObjectId(const char* objectStart, const char* objectEnd, WCHAR* out, int cap) {
    const char* keys[] = {
        "id",
        "mod_id",
        "modId",
        "workshop_id",
        "workshopId",
        "steam_id",
        "steamId",
        "published_file_id",
        "publishedFileId"
    };
    for (int i = 0; i < (int)(sizeof(keys) / sizeof(keys[0])); ++i) {
        if (JsonStringInObject(objectStart, objectEnd, keys[i], out, cap)) return TRUE;
    }
    return FALSE;
}

static BOOL JsonDependencyObjectMinVersion(const char* objectStart, const char* objectEnd, WCHAR* out, int cap) {
    const char* keys[] = {
        "min_version",
        "minVersion",
        "minimum_version",
        "minimumVersion",
        "version_min",
        "versionMin",
        "required_version",
        "requiredVersion"
    };
    for (int i = 0; i < (int)(sizeof(keys) / sizeof(keys[0])); ++i) {
        if (JsonStringInObject(objectStart, objectEnd, keys[i], out, cap)) return TRUE;
    }
    if (out && cap > 0) out[0] = 0;
    return FALSE;
}

static void JsonMinGameVersion(const char* json, WCHAR* out, int cap) {
    const char* keys[] = {
        "min_game_version",
        "minGameVersion",
        "minimum_game_version",
        "minimumGameVersion",
        "game_version_min",
        "gameVersionMin",
        "required_game_version",
        "requiredGameVersion"
    };
    if (out && cap > 0) out[0] = 0;
    for (int i = 0; i < (int)(sizeof(keys) / sizeof(keys[0])); ++i) {
        if (JsonStringValue(json, keys[i], out, cap) && out[0]) return;
    }
}

static void JsonDependencyIdsFromArray(const char* p, WCHAR deps[MAX_DEPS][MAX_TEXT], int* depCount) {
    if (!p) return;
    p = strchr(p, '[');
    if (!p) return;
    const char* end = strchr(p, ']');
    if (!end) return;

    while (p && p < end && *depCount < MAX_DEPS) {
        p = SkipWs(p + 1);
        if (p >= end) break;
        if (*p == ',') {
            continue;
        }
        if (*p == '{') {
            const char* objectEnd = strchr(p, '}');
            if (!objectEnd || objectEnd > end) objectEnd = end;
            if (JsonDependencyObjectIsOptional(p, objectEnd)) {
                p = objectEnd + 1;
                continue;
            }
            if (JsonDependencyObjectId(p, objectEnd, deps[*depCount], MAX_TEXT)) {
                ++(*depCount);
            }
            p = objectEnd + 1;
            continue;
        }
        if (*p == '"') {
            const char* value = p + 1;
            const char* start = value;
            while (*value && value < end && *value != '"') {
                if (*value == '\\' && value[1]) value += 2;
                else ++value;
            }
            if (value > start) {
                Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
                if (deps[*depCount][0]) ++(*depCount);
            }
            p = value + 1;
            continue;
        }
        ++p;
#if 0
        const char* idKey = strstr(p, "\"id\"");
        const char* nextString = strchr(p + 1, '"');
        if ((!idKey || idKey > end) && nextString && nextString < end) {
            const char* value = nextString + 1;
            const char* start = value;
            while (*value && value < end && *value != '"') {
                if (*value == '\\' && value[1]) value += 2;
                else ++value;
            }
            if (value > start) {
                Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
                if (deps[*depCount][0]) ++(*depCount);
            }
            p = value + 1;
            continue;
        }
        if (!idKey || idKey > end) break;
        const char* colon = strchr(idKey, ':');
        if (!colon || colon > end) break;
        const char* value = SkipWs(colon + 1);
        if (*value != '"') {
            p = idKey + 4;
            continue;
        }
        ++value;
        const char* start = value;
        while (*value && value < end && *value != '"') {
            if (*value == '\\' && value[1]) value += 2;
            else ++value;
        }
        if (value > start) {
            Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
            if (deps[*depCount][0]) ++(*depCount);
        }
        p = value + 1;
#endif
    }
}

static void JsonDependencyIdFromObject(const char* p, const char* objectEnd, WCHAR deps[MAX_DEPS][MAX_TEXT], int* depCount) {
    if (!p || !objectEnd || *depCount >= MAX_DEPS) return;
    if (JsonDependencyObjectId(p, objectEnd, deps[*depCount], MAX_TEXT)) ++(*depCount);
}

static void JsonDependencyIdFromSingleValue(const char* p, WCHAR deps[MAX_DEPS][MAX_TEXT], int* depCount) {
    if (!p || *depCount >= MAX_DEPS) return;
    const char* colon = strchr(p, ':');
    if (!colon) return;
    const char* value = SkipWs(colon + 1);
    if (*value == '"') {
        ++value;
        const char* start = value;
        while (*value && *value != '"') {
            if (*value == '\\' && value[1]) value += 2;
            else ++value;
        }
        if (value > start) {
            Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
            if (deps[*depCount][0]) ++(*depCount);
        }
        return;
    }
    if (*value == '{') {
        const char* objectEnd = strchr(value, '}');
        if (objectEnd && !JsonDependencyObjectIsOptional(value, objectEnd)) {
            JsonDependencyIdFromObject(value, objectEnd, deps, depCount);
        }
    }
}

static void JsonDependencyVersionRequirementsFromArray(const char* p, WCHAR deps[MAX_DEPS][MAX_TEXT], WCHAR minVersions[MAX_DEPS][MAX_TEXT], int* depCount) {
    if (!p) return;
    p = strchr(p, '[');
    if (!p) return;
    const char* end = strchr(p, ']');
    if (!end) return;

    while (p && p < end && *depCount < MAX_DEPS) {
        p = SkipWs(p + 1);
        if (p >= end) break;
        if (*p == ',') continue;
        if (*p == '{') {
            const char* objectEnd = strchr(p, '}');
            if (!objectEnd || objectEnd > end) objectEnd = end;
            if (!JsonDependencyObjectIsOptional(p, objectEnd)) {
                WCHAR id[MAX_TEXT] = L"";
                WCHAR minVersion[MAX_TEXT] = L"";
                if (JsonDependencyObjectId(p, objectEnd, id, _countof(id)) &&
                    JsonDependencyObjectMinVersion(p, objectEnd, minVersion, _countof(minVersion))) {
                    wcsncpy(deps[*depCount], id, MAX_TEXT - 1);
                    deps[*depCount][MAX_TEXT - 1] = 0;
                    wcsncpy(minVersions[*depCount], minVersion, MAX_TEXT - 1);
                    minVersions[*depCount][MAX_TEXT - 1] = 0;
                    ++(*depCount);
                }
            }
            p = objectEnd + 1;
            continue;
        }
        ++p;
    }
}

static void JsonDependencyVersionRequirementFromSingleValue(const char* p, WCHAR deps[MAX_DEPS][MAX_TEXT], WCHAR minVersions[MAX_DEPS][MAX_TEXT], int* depCount) {
    if (!p || *depCount >= MAX_DEPS) return;
    const char* colon = strchr(p, ':');
    if (!colon) return;
    const char* value = SkipWs(colon + 1);
    if (*value != '{') return;
    const char* objectEnd = strchr(value, '}');
    if (!objectEnd || JsonDependencyObjectIsOptional(value, objectEnd)) return;
    WCHAR id[MAX_TEXT] = L"";
    WCHAR minVersion[MAX_TEXT] = L"";
    if (JsonDependencyObjectId(value, objectEnd, id, _countof(id)) &&
        JsonDependencyObjectMinVersion(value, objectEnd, minVersion, _countof(minVersion))) {
        wcsncpy(deps[*depCount], id, MAX_TEXT - 1);
        deps[*depCount][MAX_TEXT - 1] = 0;
        wcsncpy(minVersions[*depCount], minVersion, MAX_TEXT - 1);
        minVersions[*depCount][MAX_TEXT - 1] = 0;
        ++(*depCount);
    }
}

static BOOL JsonBoolAfterId(const char* json, const WCHAR* id) {
    if (!json || !id || !id[0]) return FALSE;
    DWORD size = (DWORD)strlen(json);
    DWORD start = 0, len = 0;
    const char* listStart = json;
    const char* listEnd = json + size;
    if (FindModListSpan(json, size, &start, &len)) {
        listStart = json + start;
        listEnd = listStart + len;
    }

    const char* p = listStart;
    while (p < listEnd) {
        if (*p != '{') {
            ++p;
            continue;
        }
        const char* objectStart = p;
        BOOL inString = FALSE, escaped = FALSE;
        int depth = 0;
        while (p < listEnd) {
            char c = *p;
            if (inString) {
                if (escaped) escaped = FALSE;
                else if (c == '\\') escaped = TRUE;
                else if (c == '"') inString = FALSE;
            } else {
                if (c == '"') inString = TRUE;
                else if (c == '{') ++depth;
                else if (c == '}') {
                    --depth;
                    if (depth == 0) {
                        ++p;
                        break;
                    }
                }
            }
            ++p;
        }
        const char* objectEnd = p;
        WCHAR objectId[MAX_TEXT] = L"";
        if (objectEnd > objectStart &&
            JsonStringInObject(objectStart, objectEnd, "id", objectId, _countof(objectId)) &&
            _wcsicmp(objectId, id) == 0) {
            BOOL enabled = FALSE;
            if (JsonBoolInObject(objectStart, objectEnd, "is_enabled", &enabled)) return enabled;
            return FALSE;
        }
    }
    return FALSE;
}

static BOOL JsonBoolValue(const char* json, const char* key) {
    json = JsonStart(json);
    char pattern[128];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);
    const char* p = strstr(json, pattern);
    if (!p) return FALSE;
    p = strchr(p + strlen(pattern), ':');
    if (!p) return FALSE;
    p = SkipWs(p + 1);
    return strncmp(p, "true", 4) == 0;
}

static BOOL FindModListSpan(const char* json, DWORD size, DWORD* start, DWORD* len) {
    char* p = strstr((char*)json, "\"mod_list\"");
    if (!p) return FALSE;
    p = strchr(p, '[');
    if (!p) return FALSE;
    DWORD depth = 0;
    BOOL inString = FALSE;
    BOOL escaped = FALSE;
    DWORD begin = (DWORD)(p - json);
    for (DWORD i = begin; i < size; ++i) {
        char c = json[i];
        if (inString) {
            if (escaped) escaped = FALSE;
            else if (c == '\\') escaped = TRUE;
            else if (c == '"') inString = FALSE;
            continue;
        }
        if (c == '"') {
            inString = TRUE;
            continue;
        }
        if (c == '[') ++depth;
        else if (c == ']') {
            if (depth == 0) return FALSE;
            --depth;
            if (depth == 0) {
                *start = begin;
                *len = i - begin + 1;
                return TRUE;
            }
        }
    }
    return FALSE;
}

static BOOL JsonObjectIdInDiscoveredMods(const char* objectStart, const char* objectEnd) {
    const char* idKey = strstr(objectStart, "\"id\"");
    if (!idKey || idKey >= objectEnd) return FALSE;
    const char* colon = strchr(idKey, ':');
    if (!colon || colon >= objectEnd) return FALSE;
    const char* value = SkipWs(colon + 1);
    if (*value != '"') return FALSE;
    ++value;
    const char* start = value;
    while (value < objectEnd && *value && *value != '"') {
        if (*value == '\\' && value[1]) value += 2;
        else ++value;
    }
    if (value <= start) return FALSE;
    WCHAR id[MAX_TEXT];
    Utf8ToWide2(start, (int)(value - start), id, _countof(id));
    for (int i = 0; i < g_modCount; ++i) {
        if (_wcsicmp(g_mods[i].id, id) == 0) return TRUE;
    }
    return FALSE;
}

static BOOL LoadSettingsOrderFromBytes(const char* json, DWORD size) {
    g_savedOrderCount = 0;
    DWORD start = 0, len = 0;
    if (!json || !FindModListSpan(json, size, &start, &len)) return FALSE;
    const char* p = json + start;
    const char* end = p + len;
    while (p < end && g_savedOrderCount < MAX_MODS) {
        if (*p != '{') {
            ++p;
            continue;
        }
        const char* objectStart = p;
        BOOL inString = FALSE, escaped = FALSE;
        int depth = 0;
        while (p < end) {
            char c = *p;
            if (inString) {
                if (escaped) escaped = FALSE;
                else if (c == '\\') escaped = TRUE;
                else if (c == '"') inString = FALSE;
            } else {
                if (c == '"') inString = TRUE;
                else if (c == '{') ++depth;
                else if (c == '}') {
                    --depth;
                    if (depth == 0) {
                        ++p;
                        break;
                    }
                }
            }
            ++p;
        }
        const char* objectEnd = p;
        WCHAR id[MAX_TEXT] = L"";
        if (objectEnd > objectStart && JsonStringInObject(objectStart, objectEnd, "id", id, _countof(id)) && id[0]) {
            BOOL exists = FALSE;
            for (int i = 0; i < g_savedOrderCount; ++i) {
                if (_wcsicmp(g_savedOrder[i], id) == 0) {
                    exists = TRUE;
                    break;
                }
            }
            if (!exists) {
                wcsncpy(g_savedOrder[g_savedOrderCount], id, MAX_TEXT - 1);
                g_savedOrder[g_savedOrderCount][MAX_TEXT - 1] = 0;
                ++g_savedOrderCount;
            }
        }
    }
    AppendLogf(L"Loaded settings.save order entries=%d", g_savedOrderCount);
    return g_savedOrderCount > 0;
}

static BOOL IsChineseLanguage(const WCHAR* lang) {
    return _wcsnicmp(lang, L"zh", 2) == 0 || _wcsnicmp(lang, L"zhs", 3) == 0 || _wcsnicmp(lang, L"zht", 3) == 0 || _wcsicmp(lang, L"chi") == 0 || _wcsicmp(lang, L"chs") == 0;
}

static void AppendLog(const WCHAR* msg) {
    WCHAR dir[MAX_PATH * 2], path[MAX_PATH * 2], line[MAX_PATH * 4];
    JoinPath(dir, _countof(dir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dir, NULL);
    JoinPath(path, _countof(path), dir, L"launcher.log");
    SYSTEMTIME st; GetLocalTime(&st);
    swprintf(line, _countof(line), L"%04d-%02d-%02d %02d:%02d:%02d %ls\r\n",
             st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, msg);

    char utf8[MAX_PATH * 12];
    int len = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8), NULL, NULL);
    if (len <= 1) return;

    HANDLE h = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        WCHAR temp[MAX_PATH * 2];
        DWORD n = GetTempPathW(_countof(temp), temp);
        if (n == 0 || n >= _countof(temp)) return;
        JoinPath(path, _countof(path), temp, L"ModTheSpire2Launcher.log");
        h = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
        if (h == INVALID_HANDLE_VALUE) return;
    }
    DWORD written = 0;
    WriteFile(h, utf8, (DWORD)(len - 1), &written, NULL);
    CloseHandle(h);
}

static void AppendLogf(const WCHAR* fmt, ...) {
    WCHAR msg[MAX_PATH * 4];
    va_list args;
    va_start(args, fmt);
    _vsnwprintf(msg, _countof(msg) - 1, fmt, args);
    msg[_countof(msg) - 1] = 0;
    va_end(args);
    AppendLog(msg);
}

static BOOL TrySetGameDir(const WCHAR* dir) {
    WCHAR exe[MAX_PATH * 2];
    JoinPath(exe, _countof(exe), dir, L"SlayTheSpire2.exe");
    if (!FileExistsW2(exe)) return FALSE;
    wcscpy(g_gameDir, dir);
    return TRUE;
}

static void FindGameDirFromAppDir(void) {
    WCHAR cur[MAX_PATH * 2];
    wcscpy(cur, g_appDir);
    for (int i = 0; i < 8 && cur[0]; ++i) {
        if (TrySetGameDir(cur)) return;
        WCHAR* name = wcsrchr(cur, L'\\');
        name = name ? name + 1 : cur;
        if (_wcsicmp(name, L"mods") == 0) {
            WCHAR parent[MAX_PATH * 2];
            wcscpy(parent, cur);
            DirName(parent);
            if (TrySetGameDir(parent)) return;
        }
        WCHAR parent[MAX_PATH * 2];
        wcscpy(parent, cur);
        DirName(parent);
        if (wcscmp(parent, cur) == 0) break;
        wcscpy(cur, parent);
    }
}

static void FindGameDirFromSteamCommand(void) {
    if (!g_steamExe[0]) return;
    WCHAR candidate[MAX_PATH * 2];
    wcsncpy(candidate, g_steamExe, _countof(candidate) - 1);
    candidate[_countof(candidate) - 1] = 0;
    DirName(candidate);
    TrySetGameDir(candidate);
}

static void FindGameDirFromWorkshopPath(void) {
    WCHAR cur[MAX_PATH * 2];
    wcsncpy(cur, g_appDir, _countof(cur) - 1);
    cur[_countof(cur) - 1] = 0;

    for (int i = 0; i < 12 && cur[0]; ++i) {
        WCHAR* name = wcsrchr(cur, L'\\');
        name = name ? name + 1 : cur;
        if (_wcsicmp(name, L"steamapps") == 0) {
            WCHAR candidate[MAX_PATH * 2];
            swprintf(candidate, _countof(candidate), L"%ls\\common\\Slay the Spire 2", cur);
            if (TrySetGameDir(candidate)) return;
        }

        WCHAR parent[MAX_PATH * 2];
        wcscpy(parent, cur);
        DirName(parent);
        if (wcscmp(parent, cur) == 0) break;
        wcscpy(cur, parent);
    }
}

static void FindGameDir(void) {
    FindGameDirFromAppDir();
    if (g_gameDir[0]) return;
    FindGameDirFromSteamCommand();
    if (g_gameDir[0]) return;
    FindGameDirFromWorkshopPath();
    if (g_gameDir[0]) return;
    const WCHAR* candidates[] = {
        L"E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2",
        L"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Slay the Spire 2",
        L"C:\\Program Files\\Steam\\steamapps\\common\\Slay the Spire 2"
    };
    for (int i = 0; i < 3; ++i) {
        WCHAR exe[MAX_PATH * 2];
        JoinPath(exe, _countof(exe), candidates[i], L"SlayTheSpire2.exe");
        if (FileExistsW2(exe)) { wcscpy(g_gameDir, candidates[i]); return; }
    }
}

static void LoadGameVersion(void) {
    g_gameVersion[0] = 0;
    if (!g_gameDir[0]) return;
    WCHAR path[MAX_PATH * 2];
    JoinPath(path, _countof(path), g_gameDir, L"release_info.json");
    DWORD size = 0;
    char* json = ReadFileBytes(path, &size);
    if (!json) {
        AppendLogf(L"Game version unavailable: %ls", path);
        return;
    }
    if (!JsonStringValue(json, "version", g_gameVersion, _countof(g_gameVersion))) {
        JsonStringValue(json, "branch", g_gameVersion, _countof(g_gameVersion));
    }
    AppendLogf(L"Detected game version=%ls", g_gameVersion[0] ? g_gameVersion : L"(unknown)");
    HeapFree(GetProcessHeap(), 0, json);
}

static void FindWorkshopDir(WCHAR* out, int cap) {
    WCHAR common[MAX_PATH * 2], steamapps[MAX_PATH * 2];
    wcscpy(common, g_gameDir); DirName(common);
    wcscpy(steamapps, common); DirName(steamapps);
    swprintf(out, cap, L"%ls\\workshop\\content\\2868840", steamapps);
}

static BOOL FindNewestSettingsFileUnder(const WCHAR* root, WCHAR* out, int cap) {
    if (!root || !root[0] || cap <= 0) return FALSE;
    out[0] = 0;
    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", root);
    FILETIME newest = {0};
    typedef struct StackItem { WCHAR path[MAX_PATH * 2]; int depth; } StackItem;
    StackItem stack[128];
    int sp = 0;
    wcscpy(stack[sp].path, root);
    stack[sp].depth = 0;
    ++sp;

    while (sp > 0) {
        StackItem item = stack[--sp];
        swprintf(search, _countof(search), L"%ls\\*", item.path);
        WIN32_FIND_DATAW fd;
        HANDLE h = FindFirstFileW(search, &fd);
        if (h == INVALID_HANDLE_VALUE) continue;
        do {
            if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
            WCHAR path[MAX_PATH * 2];
            swprintf(path, _countof(path), L"%ls\\%ls", item.path, fd.cFileName);
            if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
                if (item.depth < 6 && sp < 128) {
                    wcscpy(stack[sp].path, path);
                    stack[sp].depth = item.depth + 1;
                    ++sp;
                }
                continue;
            }

            if (_wcsicmp(fd.cFileName, L"settings.save") == 0) {
                WIN32_FILE_ATTRIBUTE_DATA ad;
                if (GetFileAttributesExW(path, GetFileExInfoStandard, &ad)) {
                    if (!g_settingsFile[0] || CompareFileTime(&ad.ftLastWriteTime, &newest) > 0) {
                        newest = ad.ftLastWriteTime;
                        wcsncpy(out, path, cap - 1);
                        out[cap - 1] = 0;
                    }
                }
            }
        } while (FindNextFileW(h, &fd));
        FindClose(h);
    }
    return out[0] != 0;
}

static void FindSettingsFile(void) {
    WCHAR roaming[MAX_PATH];
    if (FAILED(SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, SHGFP_TYPE_CURRENT, roaming))) return;
    WCHAR root[MAX_PATH * 2];
    swprintf(root, _countof(root), L"%ls\\SlayTheSpire2", roaming);
    FindNewestSettingsFileUnder(root, g_settingsFile, _countof(g_settingsFile));
}

static void DetectLanguage(void) {
    g_chinese = FALSE;
    DWORD size = 0;
    char* json = ReadFileBytes(g_settingsFile, &size);
    if (!json) return;
    WCHAR lang[64] = L"";
    JsonStringValue(json, "language", lang, _countof(lang));
    g_chinese = IsChineseLanguage(lang);
    HeapFree(GetProcessHeap(), 0, json);
}

static int HasModId(const WCHAR* id) {
    for (int i = 0; i < g_modCount; ++i) if (_wcsicmp(g_mods[i].id, id) == 0) return 1;
    return 0;
}

static BOOL IsManifestCandidateFile(const WCHAR* name) {
    const WCHAR* ext = wcsrchr(name, L'.');
    return ext && (_wcsicmp(ext, L".json") == 0 || _wcsicmp(ext, L".manifest") == 0);
}

static BOOL IsJsonManifestFile(const WCHAR* name) {
    const WCHAR* ext = wcsrchr(name, L'.');
    return ext && _wcsicmp(ext, L".json") == 0;
}

static BOOL IsSidecarManifestFile(const WCHAR* name) {
    const WCHAR* ext = wcsrchr(name, L'.');
    return ext && _wcsicmp(ext, L".manifest") == 0;
}

static void FileStem(const WCHAR* path, WCHAR* out, int cap) {
    if (!out || cap <= 0) return;
    out[0] = 0;
    const WCHAR* name = wcsrchr(path, L'\\');
    const WCHAR* slash = wcsrchr(path, L'/');
    if (!name || (slash && slash > name)) name = slash;
    name = name ? name + 1 : path;
    wcsncpy(out, name, cap - 1);
    out[cap - 1] = 0;
    WCHAR* dot = wcsrchr(out, L'.');
    if (dot) *dot = 0;
}

static BOOL ContainsPayloadFileRecursive(const WCHAR* root, const WCHAR* ext) {
    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", root);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h == INVALID_HANDLE_VALUE) return FALSE;
    BOOL found = FALSE;
    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
        WCHAR path[MAX_PATH * 2];
        swprintf(path, _countof(path), L"%ls\\%ls", root, fd.cFileName);
        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            if (_wcsicmp(fd.cFileName, L"ModTheSpire2Data") == 0) continue;
            if (ContainsPayloadFileRecursive(path, ext)) {
                found = TRUE;
                break;
            }
        } else {
            const WCHAR* fileExt = wcsrchr(fd.cFileName, L'.');
            if (fileExt && _wcsicmp(fileExt, ext) == 0) {
                found = TRUE;
                break;
            }
        }
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    return found;
}

static BOOL IsPlausibleManifestCandidate(const WCHAR* path, const WCHAR* id, const WCHAR* pckName, BOOL declaresPayload) {
    if (IsSidecarManifestFile(path)) return TRUE;
    const WCHAR* leaf = wcsrchr(path, L'\\');
    const WCHAR* slash = wcsrchr(path, L'/');
    if (!leaf || (slash && slash > leaf)) leaf = slash;
    leaf = leaf ? leaf + 1 : path;
    if (_wcsicmp(leaf, L"mod_manifest.json") == 0 || _wcsicmp(leaf, L"mod_mainfest.json") == 0) return TRUE;

    WCHAR stem[MAX_TEXT];
    FileStem(path, stem, _countof(stem));
    if (id && id[0] && _wcsicmp(stem, id) == 0) return TRUE;
    if (pckName && pckName[0] && _wcsicmp(stem, pckName) == 0) return TRUE;
    if (declaresPayload) return TRUE;
    if (DirectoryHasSidecarManifest(path)) return TRUE;

    WCHAR dir[MAX_PATH * 2];
    wcsncpy(dir, path, _countof(dir) - 1);
    dir[_countof(dir) - 1] = 0;
    DirName(dir);
    const WCHAR* parent = wcsrchr(dir, L'\\');
    const WCHAR* parentSlash = wcsrchr(dir, L'/');
    if (!parent || (parentSlash && parentSlash > parent)) parent = parentSlash;
    parent = parent ? parent + 1 : dir;
    if (parent && parent[0] && _wcsicmp(stem, parent) == 0) return TRUE;
    return ContainsPayloadFileRecursive(dir, L".dll") || ContainsPayloadFileRecursive(dir, L".pck");
}

static void ExtractWorkshopIdFromPath(const WCHAR* path, WCHAR* out, int cap) {
    if (!out || cap <= 0) return;
    out[0] = 0;
    const WCHAR* marker = wcsstr(path, L"\\2868840\\");
    if (!marker) marker = wcsstr(path, L"/2868840/");
    if (!marker) return;
    marker += 9;
    const WCHAR* end = marker;
    while (*end && *end != L'\\' && *end != L'/') ++end;
    if (end == marker || end - marker >= cap) return;
    for (const WCHAR* p = marker; p < end; ++p) {
        if (*p < L'0' || *p > L'9') return;
    }
    wcsncpy(out, marker, (size_t)(end - marker));
    out[end - marker] = 0;
}

static BOOL DirectoryHasOtherIdJson(const WCHAR* path) {
    WCHAR dir[MAX_PATH * 2];
    wcsncpy(dir, path, _countof(dir) - 1);
    dir[_countof(dir) - 1] = 0;
    DirName(dir);

    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", dir);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h == INVALID_HANDLE_VALUE) return FALSE;

    BOOL found = FALSE;
    do {
        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) continue;
        if (!IsManifestCandidateFile(fd.cFileName)) continue;
        WCHAR candidate[MAX_PATH * 2];
        swprintf(candidate, _countof(candidate), L"%ls\\%ls", dir, fd.cFileName);
        if (_wcsicmp(candidate, path) == 0) continue;

        DWORD size = 0;
        char* json = ReadFileBytes(candidate, &size);
        if (!json) continue;
        WCHAR otherId[MAX_TEXT] = L"";
        if (JsonStringValue(json, "id", otherId, _countof(otherId)) && otherId[0]) {
            found = TRUE;
            HeapFree(GetProcessHeap(), 0, json);
            break;
        }
        HeapFree(GetProcessHeap(), 0, json);
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    return found;
}

static BOOL DirectoryHasSidecarManifest(const WCHAR* path) {
    WCHAR dir[MAX_PATH * 2];
    wcsncpy(dir, path, _countof(dir) - 1);
    dir[_countof(dir) - 1] = 0;
    DirName(dir);

    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", dir);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h == INVALID_HANDLE_VALUE) return FALSE;

    BOOL found = FALSE;
    do {
        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) continue;
        WCHAR candidate[MAX_PATH * 2];
        swprintf(candidate, _countof(candidate), L"%ls\\%ls", dir, fd.cFileName);
        if (_wcsicmp(candidate, path) == 0) continue;
        if (_wcsicmp(fd.cFileName, L"mod_manifest.json") == 0 ||
            _wcsicmp(fd.cFileName, L"mod_mainfest.json") == 0 ||
            IsSidecarManifestFile(fd.cFileName)) {
            found = TRUE;
            break;
        }
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    return found;
}

static void AddModFromJson(const WCHAR* path, const WCHAR* source) {
    if (g_modCount >= MAX_MODS) return;
    DWORD size = 0;
    char* json = ReadFileBytes(path, &size);
    if (!json) return;
    WCHAR id[MAX_TEXT] = L"", name[MAX_TEXT] = L"", pckName[MAX_TEXT] = L"";
    if (!JsonStringValue(json, "id", id, _countof(id)) || !id[0]) {
        if (IsJsonManifestFile(path) && !DirectoryHasOtherIdJson(path)) {
            JsonStringValue(json, "pck_name", pckName, _countof(pckName));
            if (pckName[0]) wcscpy(id, pckName);
        }
    } else {
        JsonStringValue(json, "pck_name", pckName, _countof(pckName));
    }
    BOOL declaresPayload = JsonBoolValue(json, "has_dll") || JsonBoolValue(json, "has_pck");
    if (id[0] && IsPlausibleManifestCandidate(path, id, pckName, declaresPayload) && !HasModId(id)) {
        JsonStringValue(json, "name", name, _countof(name));
        if (!name[0]) wcscpy(name, id);
        wcscpy(g_mods[g_modCount].id, id);
        wcscpy(g_mods[g_modCount].name, name);
        JsonStringValue(json, "version", g_mods[g_modCount].version, _countof(g_mods[g_modCount].version));
        JsonMinGameVersion(json, g_mods[g_modCount].minGameVersion, _countof(g_mods[g_modCount].minGameVersion));
        wcscpy(g_mods[g_modCount].source, source);
        wcsncpy(g_mods[g_modCount].manifest, path, _countof(g_mods[g_modCount].manifest) - 1);
        ExtractWorkshopIdFromPath(path, g_mods[g_modCount].workshopId, _countof(g_mods[g_modCount].workshopId));
        JsonDependencyIds(json, g_mods[g_modCount].deps, &g_mods[g_modCount].depCount);
        JsonDependencyVersionRequirements(json, g_mods[g_modCount].deps, g_mods[g_modCount].depMinVersions, &g_mods[g_modCount].depCount);
        JsonLoadAfterIds(json, g_mods[g_modCount].orderAfter, &g_mods[g_modCount].orderAfterCount);
        JsonLoadBeforeIds(json, g_mods[g_modCount].orderBefore, &g_mods[g_modCount].orderBeforeCount);
        g_mods[g_modCount].affectsGameplay = JsonBoolValue(json, "affects_gameplay");
        ++g_modCount;
    }
    HeapFree(GetProcessHeap(), 0, json);
}

static void DiscoverRoot(const WCHAR* root, const WCHAR* source) {
    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", root);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h == INVALID_HANDLE_VALUE) {
        AppendLogf(L"Scan skipped: %ls root not found: %ls", source, root);
        return;
    }
    int before = g_modCount;
    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
        WCHAR path[MAX_PATH * 2];
        swprintf(path, _countof(path), L"%ls\\%ls", root, fd.cFileName);
        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            if (_wcsicmp(fd.cFileName, L"ModTheSpire2Data") == 0) {
                AppendLogf(L"Scan skipped runtime data folder: %ls", path);
                continue;
            }
            DiscoverRoot(path, source);
        } else {
            if (IsManifestCandidateFile(fd.cFileName)) {
                AddModFromJson(path, source);
            }
        }
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    AppendLogf(L"Scanned %ls root: %ls found=%d", source, root, g_modCount - before);
}

static int CmpMods(const void* a, const void* b) {
    return _wcsicmp(((const ModInfo*)a)->name, ((const ModInfo*)b)->name);
}

static int FindModIndexById(const WCHAR* id) {
    for (int i = 0; i < g_modCount; ++i) {
        if (_wcsicmp(g_mods[i].id, id) == 0) return i;
    }
    return -1;
}

static int FindUniqueModIndexByName(const WCHAR* name) {
    int found = -1;
    if (!name || !name[0]) return -1;
    for (int i = 0; i < g_modCount; ++i) {
        if (_wcsicmp(g_mods[i].name, name) == 0) {
            if (found >= 0) return -1;
            found = i;
        }
    }
    return found;
}

static int FindUniqueModIndexByWorkshopId(const WCHAR* workshopId) {
    int found = -1;
    if (!workshopId || !workshopId[0]) return -1;
    for (int i = 0; i < g_modCount; ++i) {
        if (_wcsicmp(g_mods[i].workshopId, workshopId) == 0) {
            if (found >= 0) return -1;
            found = i;
        }
    }
    return found;
}

static void CanonicalizeReferenceIds(WCHAR refs[MAX_DEPS][MAX_TEXT], int refCount, const WCHAR* label, const WCHAR* ownerId) {
    for (int d = 0; d < refCount; ++d) {
        if (FindModIndexById(refs[d]) >= 0) continue;
        int byName = FindUniqueModIndexByName(refs[d]);
        if (byName >= 0) {
            AppendLogf(L"%ls alias resolved: %ls -> %ls (%ls)", label, ownerId, g_mods[byName].id, refs[d]);
            wcsncpy(refs[d], g_mods[byName].id, MAX_TEXT - 1);
            refs[d][MAX_TEXT - 1] = 0;
            continue;
        }
        int byWorkshopId = FindUniqueModIndexByWorkshopId(refs[d]);
        if (byWorkshopId >= 0) {
            AppendLogf(L"%ls workshop alias resolved: %ls -> %ls (%ls)", label, ownerId, g_mods[byWorkshopId].id, refs[d]);
            wcsncpy(refs[d], g_mods[byWorkshopId].id, MAX_TEXT - 1);
            refs[d][MAX_TEXT - 1] = 0;
        }
    }
}

static void CanonicalizeDependencyVersionRequirements(int modIndex) {
    if (modIndex < 0 || modIndex >= g_modCount) return;
    for (int d = 0; d < g_mods[modIndex].depCount; ++d) {
        if (!g_mods[modIndex].depMinVersions[d][0]) continue;
        WCHAR before[MAX_TEXT];
        wcsncpy(before, g_mods[modIndex].deps[d], _countof(before) - 1);
        before[_countof(before) - 1] = 0;
        CanonicalizeReferenceIds(&g_mods[modIndex].deps[d], 1, L"Dependency version", g_mods[modIndex].id);
        if (_wcsicmp(before, g_mods[modIndex].deps[d]) != 0) {
            AppendLogf(L"Dependency version requirement alias resolved: %ls -> %ls (%ls >= %ls)", g_mods[modIndex].id, g_mods[modIndex].deps[d], before, g_mods[modIndex].depMinVersions[d]);
        }
    }
}

static void CanonicalizeDependencyIds(void) {
    for (int i = 0; i < g_modCount; ++i) {
        CanonicalizeReferenceIds(g_mods[i].deps, g_mods[i].depCount, L"Dependency", g_mods[i].id);
        CanonicalizeDependencyVersionRequirements(i);
        CanonicalizeReferenceIds(g_mods[i].orderAfter, g_mods[i].orderAfterCount, L"Load-after", g_mods[i].id);
        CanonicalizeReferenceIds(g_mods[i].orderBefore, g_mods[i].orderBeforeCount, L"Load-before", g_mods[i].id);
    }
}

static BOOL IdInSavedOrder(const WCHAR* id) {
    for (int i = 0; i < g_savedOrderCount; ++i) {
        if (_wcsicmp(g_savedOrder[i], id) == 0) return TRUE;
    }
    return FALSE;
}

static void GetLoadOrderPath(WCHAR* out, int cap) {
    WCHAR dataDir[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(out, cap, dataDir, L"load-order.txt");
}

static void GetEnabledModsPath(WCHAR* out, int cap) {
    WCHAR dataDir[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(out, cap, dataDir, L"enabled-mods.txt");
}

static void GetProfilesDir(WCHAR* out, int cap) {
    WCHAR dataDir[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(out, cap, dataDir, L"order-profiles");
    CreateDirectoryW(out, NULL);
}

static void GetFileBackupsDir(WCHAR* out, int cap) {
    WCHAR dataDir[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(out, cap, dataDir, L"file-backups");
    CreateDirectoryW(out, NULL);
}

static void SafeFileStem(const WCHAR* path, WCHAR* out, int cap) {
    const WCHAR* name = wcsrchr(path, L'\\');
    name = name ? name + 1 : path;
    int j = 0;
    for (int i = 0; name && name[i] && j < cap - 1; ++i) {
        WCHAR c = name[i];
        if ((c >= L'a' && c <= L'z') || (c >= L'A' && c <= L'Z') || (c >= L'0' && c <= L'9') || c == L'-' || c == L'_' || c == L'.') {
            out[j++] = c;
        } else {
            out[j++] = L'_';
        }
    }
    out[j] = 0;
    if (!out[0]) wcsncpy(out, L"data-file", cap - 1);
    out[cap - 1] = 0;
}

static BOOL BackupDataFile(const WCHAR* path) {
    if (!FileExistsW2(path)) return TRUE;
    WCHAR dir[MAX_PATH * 2], stem[MAX_PROFILE + 64], name[MAX_PATH], dst[MAX_PATH * 2];
    GetFileBackupsDir(dir, _countof(dir));
    SafeFileStem(path, stem, _countof(stem));
    SYSTEMTIME st;
    GetLocalTime(&st);
    swprintf(name, _countof(name), L"%ls.%04d%02d%02d-%02d%02d%02d.bak", stem, st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
    JoinPath(dst, _countof(dst), dir, name);
    BOOL ok = CopyFileW(path, dst, FALSE);
    AppendLogf(ok ? L"Backed up data file: %ls" : L"Failed to back up data file: %ls", path);
    return ok;
}

static void SanitizeProfileName(const WCHAR* input, WCHAR* out, int cap) {
    int j = 0;
    for (int i = 0; input && input[i] && j < cap - 1; ++i) {
        WCHAR c = input[i];
        if (c < 32 || wcschr(L"<>:\"/\\|?*", c)) continue;
        out[j++] = c;
    }
    while (j > 0 && (out[j - 1] == L' ' || out[j - 1] == L'.')) --j;
    out[j] = 0;
    if (!out[0]) wcsncpy(out, L"default", cap - 1);
    out[cap - 1] = 0;
}

static void GetProfilePath(const WCHAR* profileName, WCHAR* out, int cap) {
    WCHAR dir[MAX_PATH * 2], safe[MAX_PROFILE], file[MAX_PROFILE + 8];
    GetProfilesDir(dir, _countof(dir));
    SanitizeProfileName(profileName, safe, _countof(safe));
    swprintf(file, _countof(file), L"%ls.txt", safe);
    JoinPath(out, cap, dir, file);
}

static void GetProfileEnabledPath(const WCHAR* profileName, WCHAR* out, int cap) {
    WCHAR dir[MAX_PATH * 2], safe[MAX_PROFILE], file[MAX_PROFILE + 20];
    GetProfilesDir(dir, _countof(dir));
    SanitizeProfileName(profileName, safe, _countof(safe));
    swprintf(file, _countof(file), L"%ls.enabled.txt", safe);
    JoinPath(out, cap, dir, file);
}

static void LoadSavedOrder(void) {
    g_savedOrderCount = 0;
    WCHAR path[MAX_PATH * 2];
    GetLoadOrderPath(path, _countof(path));
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) return;
    char* p = data;
    if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF) {
        p += 3;
    }
    while (*p && g_savedOrderCount < MAX_MODS) {
        while (*p == '\r' || *p == '\n' || *p == ' ' || *p == '\t') ++p;
        char* start = p;
        while (*p && *p != '\r' && *p != '\n') ++p;
        char* end = p;
        while (end > start && (end[-1] == ' ' || end[-1] == '\t')) --end;
        if (end > start) {
            Utf8ToWide2(start, (int)(end - start), g_savedOrder[g_savedOrderCount], MAX_TEXT);
            if (g_savedOrder[g_savedOrderCount][0]) ++g_savedOrderCount;
        }
    }
    HeapFree(GetProcessHeap(), 0, data);
    AppendLogf(L"Loaded custom order entries=%d", g_savedOrderCount);
}

static void LoadSavedEnabled(void) {
    g_savedEnabledCount = 0;
    WCHAR path[MAX_PATH * 2];
    GetEnabledModsPath(path, _countof(path));
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) return;
    char* p = data;
    if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF) {
        p += 3;
    }
    while (*p && g_savedEnabledCount < MAX_MODS) {
        while (*p == '\r' || *p == '\n' || *p == ' ' || *p == '\t') ++p;
        char* start = p;
        while (*p && *p != '\r' && *p != '\n') ++p;
        char* end = p;
        while (end > start && (end[-1] == ' ' || end[-1] == '\t')) --end;
        if (end > start) {
            Utf8ToWide2(start, (int)(end - start), g_savedEnabled[g_savedEnabledCount], MAX_TEXT);
            if (g_savedEnabled[g_savedEnabledCount][0]) ++g_savedEnabledCount;
        }
    }
    HeapFree(GetProcessHeap(), 0, data);
    AppendLogf(L"Loaded saved enabled entries=%d", g_savedEnabledCount);
}

static BOOL IdInSavedEnabled(const WCHAR* id) {
    for (int i = 0; i < g_savedEnabledCount; ++i) {
        if (_wcsicmp(g_savedEnabled[i], id) == 0) return TRUE;
    }
    return FALSE;
}

static BOOL SaveCurrentEnabledToPath(const WCHAR* path) {
    if (!BackupDataFile(path)) {
        return FALSE;
    }
    HANDLE h = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        return FALSE;
    }

    for (int i = 0; i < g_modCount; ++i) {
        if (!ListView_GetCheckState(g_list, i)) continue;
        char id8[MAX_TEXT * 4];
        int len = WideToUtf82(g_mods[i].id, id8, sizeof(id8));
        if (len > 1) {
            DWORD written = 0;
            WriteFile(h, id8, (DWORD)(len - 1), &written, NULL);
            WriteFile(h, "\r\n", 2, &written, NULL);
        }
    }
    CloseHandle(h);
    return TRUE;
}

static BOOL SaveCurrentEnabled(void) {
    WCHAR path[MAX_PATH * 2];
    GetEnabledModsPath(path, _countof(path));
    if (!SaveCurrentEnabledToPath(path)) {
        return FALSE;
    }
    LoadSavedEnabled();
    return TRUE;
}

static BOOL SaveOrderToPath(const WCHAR* path) {
    if (!BackupDataFile(path)) {
        return FALSE;
    }
    HANDLE h = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        return FALSE;
    }

    for (int i = 0; i < g_modCount; ++i) {
        char id8[MAX_TEXT * 4];
        int len = WideToUtf82(g_mods[i].id, id8, sizeof(id8));
        if (len > 1) {
            DWORD written = 0;
            WriteFile(h, id8, (DWORD)(len - 1), &written, NULL);
            WriteFile(h, "\r\n", 2, &written, NULL);
        }
    }
    CloseHandle(h);
    return TRUE;
}

static BOOL LoadEnabledFromPathToList(const WCHAR* path) {
    if (!g_list) return FALSE;
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) return FALSE;
    WCHAR (*ids)[MAX_TEXT] = (WCHAR (*)[MAX_TEXT])HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(WCHAR) * MAX_MODS * MAX_TEXT);
    if (!ids) {
        HeapFree(GetProcessHeap(), 0, data);
        return FALSE;
    }

    int idCount = 0;
    char* p = data;
    if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF) p += 3;
    while (*p && idCount < MAX_MODS) {
        while (*p == '\r' || *p == '\n' || *p == ' ' || *p == '\t') ++p;
        char* start = p;
        while (*p && *p != '\r' && *p != '\n') ++p;
        char* end = p;
        while (end > start && (end[-1] == ' ' || end[-1] == '\t')) --end;
        if (end > start) {
            Utf8ToWide2(start, (int)(end - start), ids[idCount], MAX_TEXT);
            if (ids[idCount][0]) ++idCount;
        }
    }
    HeapFree(GetProcessHeap(), 0, data);

    g_enforcingChecks = TRUE;
    for (int i = 0; i < g_modCount; ++i) {
        BOOL enabled = FALSE;
        for (int id = 0; id < idCount; ++id) {
            if (_wcsicmp(g_mods[i].id, ids[id]) == 0) {
                enabled = TRUE;
                break;
            }
        }
        ListView_SetCheckState(g_list, i, enabled);
    }
    int pruned = PruneInvalidChecks();
    g_enforcingChecks = FALSE;
    if (pruned > 0 && g_status) {
        WCHAR status[MAX_TEXT];
        swprintf(status, _countof(status), L"Profile enabled mods loaded. Removed %d invalid selection(s).", pruned);
        SetWindowTextW(g_status, status);
    }

    HeapFree(GetProcessHeap(), 0, ids);
    return TRUE;
}

static void SaveCurrentOrder(void) {
    SaveNamedProfile();
}

static int SavedOrderRank(const WCHAR* id) {
    for (int i = 0; i < g_savedOrderCount; ++i) {
        if (_wcsicmp(g_savedOrder[i], id) == 0) return i;
    }
    return 1000000;
}

static int CmpModsSavedOrder(const void* a, const void* b) {
    const ModInfo* ma = (const ModInfo*)a;
    const ModInfo* mb = (const ModInfo*)b;
    int ra = SavedOrderRank(ma->id);
    int rb = SavedOrderRank(mb->id);
    if (ra != rb) return ra - rb;
    return _wcsicmp(ma->name, mb->name);
}

static void ApplySavedOrderBeforeDependencySort(void) {
    if (g_savedOrderCount <= 0) return;
    qsort(g_mods, g_modCount, sizeof(ModInfo), CmpModsSavedOrder);
}

static int FirstKnownDepIndex(const ModInfo* mod) {
    for (int i = 0; i < mod->depCount; ++i) {
        int dep = FindModIndexById(mod->deps[i]);
        if (dep >= 0) return dep;
    }
    for (int i = 0; i < mod->orderAfterCount; ++i) {
        int dep = FindModIndexById(mod->orderAfter[i]);
        if (dep >= 0) return dep;
    }
    for (int i = 0; i < g_modCount; ++i) {
        for (int d = 0; d < g_mods[i].orderBeforeCount; ++d) {
            if (_wcsicmp(g_mods[i].orderBefore[d], mod->id) == 0) {
                return i;
            }
        }
    }
    return -1;
}

static BOOL DependsOn(const ModInfo* mod, const WCHAR* depId) {
    for (int i = 0; i < mod->depCount; ++i) {
        if (_wcsicmp(mod->deps[i], depId) == 0) return TRUE;
    }
    return FALSE;
}

static BOOL OrdersAfter(const ModInfo* mod, const WCHAR* depId) {
    if (DependsOn(mod, depId)) return TRUE;
    for (int i = 0; i < mod->orderAfterCount; ++i) {
        if (_wcsicmp(mod->orderAfter[i], depId) == 0) return TRUE;
    }
    int depIndex = FindModIndexById(depId);
    if (depIndex >= 0) {
        for (int i = 0; i < g_mods[depIndex].orderBeforeCount; ++i) {
            if (_wcsicmp(g_mods[depIndex].orderBefore[i], mod->id) == 0) return TRUE;
        }
    }
    return FALSE;
}

static void ApplyVisualDependencyIndentPreservingOrder(void) {
    for (int i = 0; i < g_modCount; ++i) {
        g_mods[i].depth = 0;
    }
    for (int pass = 0; pass < MAX_DEPS; ++pass) {
        BOOL changed = FALSE;
        for (int i = 0; i < g_modCount; ++i) {
            int depth = 0;
            for (int j = 0; j < i; ++j) {
                if (OrdersAfter(&g_mods[i], g_mods[j].id)) {
                    int candidate = g_mods[j].depth + 1;
                    if (candidate > depth) depth = candidate;
                }
            }
            if (depth > 4) depth = 4;
            if (depth != g_mods[i].depth) {
                g_mods[i].depth = depth;
                changed = TRUE;
            }
        }
        if (!changed) break;
    }
}

static BOOL ValidateDependencyOrderCore(WCHAR* outMessage, int cap, BOOL selectedOnly) {
    for (int i = 0; i < g_modCount; ++i) {
        if (selectedOnly && g_list && !ListView_GetCheckState(g_list, i)) continue;
        for (int d = 0; d < g_mods[i].depCount; ++d) {
            int depIndex = FindModIndexById(g_mods[i].deps[d]);
            if (selectedOnly && depIndex >= 0 && g_list && !ListView_GetCheckState(g_list, depIndex)) continue;
            if (depIndex >= 0 && depIndex > i) {
                if (outMessage && cap > 0) {
                    const WCHAR* modName = g_mods[i].name[0] ? g_mods[i].name : g_mods[i].id;
                    const WCHAR* depName = g_mods[depIndex].name[0] ? g_mods[depIndex].name : g_mods[depIndex].id;
                    swprintf(outMessage, cap, L"Invalid order: %ls must load before %ls.", depName, modName);
                }
                return FALSE;
            }
        }
        for (int d = 0; d < g_mods[i].orderAfterCount; ++d) {
            int depIndex = FindModIndexById(g_mods[i].orderAfter[d]);
            if (selectedOnly && depIndex >= 0 && g_list && !ListView_GetCheckState(g_list, depIndex)) continue;
            if (depIndex >= 0 && depIndex > i) {
                if (outMessage && cap > 0) {
                    const WCHAR* modName = g_mods[i].name[0] ? g_mods[i].name : g_mods[i].id;
                    const WCHAR* depName = g_mods[depIndex].name[0] ? g_mods[depIndex].name : g_mods[depIndex].id;
                    swprintf(outMessage, cap, L"Invalid order: %ls should load before %ls.", depName, modName);
                }
                return FALSE;
            }
        }
        for (int d = 0; d < g_mods[i].orderBeforeCount; ++d) {
            int targetIndex = FindModIndexById(g_mods[i].orderBefore[d]);
            if (selectedOnly && targetIndex >= 0 && g_list && !ListView_GetCheckState(g_list, targetIndex)) continue;
            if (targetIndex >= 0 && targetIndex < i) {
                if (outMessage && cap > 0) {
                    const WCHAR* modName = g_mods[i].name[0] ? g_mods[i].name : g_mods[i].id;
                    const WCHAR* targetName = g_mods[targetIndex].name[0] ? g_mods[targetIndex].name : g_mods[targetIndex].id;
                    swprintf(outMessage, cap, L"Invalid order: %ls should load before %ls.", modName, targetName);
                }
                return FALSE;
            }
        }
    }
    if (outMessage && cap > 0) outMessage[0] = 0;
    return TRUE;
}

static BOOL ValidateDependencyOrder(WCHAR* outMessage, int cap) {
    return ValidateDependencyOrderCore(outMessage, cap, FALSE);
}

static BOOL ValidateSelectedDependencyOrder(WCHAR* outMessage, int cap) {
    return ValidateDependencyOrderCore(outMessage, cap, TRUE);
}

static BOOL RepairDependencyOrderInPlace(void) {
    BOOL changed = TRUE;
    BOOL repaired = FALSE;
    int guard = 0;
    while (changed && guard++ < MAX_MODS) {
        changed = FALSE;
        for (int i = 0; i < g_modCount; ++i) {
            for (int d = 0; d < g_mods[i].depCount + g_mods[i].orderAfterCount; ++d) {
                const WCHAR* refId = d < g_mods[i].depCount ? g_mods[i].deps[d] : g_mods[i].orderAfter[d - g_mods[i].depCount];
                int depIndex = FindModIndexById(refId);
                if (depIndex > i) {
                    ModInfo dep = g_mods[depIndex];
                    for (int j = depIndex; j > i; --j) {
                        g_mods[j] = g_mods[j - 1];
                    }
                    g_mods[i] = dep;
                    changed = TRUE;
                    repaired = TRUE;
                    break;
                }
            }
            if (!changed) {
                for (int d = 0; d < g_mods[i].orderBeforeCount; ++d) {
                    int targetIndex = FindModIndexById(g_mods[i].orderBefore[d]);
                    if (targetIndex >= 0 && targetIndex < i) {
                        ModInfo mod = g_mods[i];
                        for (int j = i; j > targetIndex; --j) {
                            g_mods[j] = g_mods[j - 1];
                        }
                        g_mods[targetIndex] = mod;
                        changed = TRUE;
                        repaired = TRUE;
                        break;
                    }
                }
            }
            if (changed) break;
        }
    }
    if (repaired) AppendLog(L"Load order adjusted to satisfy dependencies");
    return repaired;
}

static void JsonDependencyIds(const char* json, WCHAR deps[MAX_DEPS][MAX_TEXT], int* depCount) {
    *depCount = 0;
    const char* keys[] = {
        "\"dependencies\"",
        "\"requires\"",
        "\"required_mods\"",
        "\"requiredMods\""
    };
    for (int i = 0; i < (int)(sizeof(keys) / sizeof(keys[0])) && *depCount < MAX_DEPS; ++i) {
        const char* p = strstr(json, keys[i]);
        if (p) {
            const char* colon = strchr(p, ':');
            const char* value = colon ? SkipWs(colon + 1) : NULL;
            if (value && *value == '[') JsonDependencyIdsFromArray(p, deps, depCount);
            else JsonDependencyIdFromSingleValue(p, deps, depCount);
        }
    }
}

static void JsonDependencyVersionRequirements(const char* json, WCHAR deps[MAX_DEPS][MAX_TEXT], WCHAR minVersions[MAX_DEPS][MAX_TEXT], int* depCount) {
    for (int i = 0; i < MAX_DEPS; ++i) minVersions[i][0] = 0;

    WCHAR reqIds[MAX_DEPS][MAX_TEXT] = {{0}};
    WCHAR reqVersions[MAX_DEPS][MAX_TEXT] = {{0}};
    int reqCount = 0;
    const char* keys[] = {
        "\"dependencies\"",
        "\"requires\"",
        "\"required_mods\"",
        "\"requiredMods\""
    };
    for (int i = 0; i < (int)(sizeof(keys) / sizeof(keys[0])) && reqCount < MAX_DEPS; ++i) {
        const char* p = strstr(json, keys[i]);
        if (p) {
            const char* colon = strchr(p, ':');
            const char* value = colon ? SkipWs(colon + 1) : NULL;
            if (value && *value == '[') JsonDependencyVersionRequirementsFromArray(p, reqIds, reqVersions, &reqCount);
            else JsonDependencyVersionRequirementFromSingleValue(p, reqIds, reqVersions, &reqCount);
        }
    }

    for (int r = 0; r < reqCount; ++r) {
        if (!reqIds[r][0] || !reqVersions[r][0]) continue;
        int found = -1;
        for (int d = 0; d < *depCount; ++d) {
            if (_wcsicmp(deps[d], reqIds[r]) == 0) {
                found = d;
                break;
            }
        }
        if (found < 0 && *depCount < MAX_DEPS) {
            found = *depCount;
            wcsncpy(deps[found], reqIds[r], MAX_TEXT - 1);
            deps[found][MAX_TEXT - 1] = 0;
            ++(*depCount);
        }
        if (found >= 0) {
            wcsncpy(minVersions[found], reqVersions[r], MAX_TEXT - 1);
            minVersions[found][MAX_TEXT - 1] = 0;
        }
    }
}

static void JsonLoadAfterIds(const char* json, WCHAR ids[MAX_DEPS][MAX_TEXT], int* idCount) {
    *idCount = 0;
    const char* keys[] = {
        "\"load_after\"",
        "\"loadAfter\""
    };
    for (int i = 0; i < (int)(sizeof(keys) / sizeof(keys[0])) && *idCount < MAX_DEPS; ++i) {
        const char* p = strstr(json, keys[i]);
        if (p) {
            const char* colon = strchr(p, ':');
            const char* value = colon ? SkipWs(colon + 1) : NULL;
            if (value && *value == '[') JsonDependencyIdsFromArray(p, ids, idCount);
            else JsonDependencyIdFromSingleValue(p, ids, idCount);
        }
    }
}

static void JsonLoadBeforeIds(const char* json, WCHAR ids[MAX_DEPS][MAX_TEXT], int* idCount) {
    *idCount = 0;
    const char* keys[] = {
        "\"load_before\"",
        "\"loadBefore\""
    };
    for (int i = 0; i < (int)(sizeof(keys) / sizeof(keys[0])) && *idCount < MAX_DEPS; ++i) {
        const char* p = strstr(json, keys[i]);
        if (p) {
            const char* colon = strchr(p, ':');
            const char* value = colon ? SkipWs(colon + 1) : NULL;
            if (value && *value == '[') JsonDependencyIdsFromArray(p, ids, idCount);
            else JsonDependencyIdFromSingleValue(p, ids, idCount);
        }
    }
}

static BOOL HasUncheckedKnownDependency(int index, WCHAR* outName, int cap) {
    if (index < 0 || index >= g_modCount) return FALSE;
    for (int d = 0; d < g_mods[index].depCount; ++d) {
        int dep = FindModIndexById(g_mods[index].deps[d]);
        if (dep >= 0 && !ListView_GetCheckState(g_list, dep)) {
            if (outName && cap > 0) {
                wcsncpy(outName, g_mods[dep].name[0] ? g_mods[dep].name : g_mods[dep].id, cap - 1);
                outName[cap - 1] = 0;
            }
            return TRUE;
        }
    }
    return FALSE;
}

static BOOL HasMissingDependency(int index, WCHAR* outName, int cap) {
    if (index < 0 || index >= g_modCount) return FALSE;
    for (int d = 0; d < g_mods[index].depCount; ++d) {
        if (FindModIndexById(g_mods[index].deps[d]) < 0) {
            if (outName && cap > 0) {
                wcsncpy(outName, g_mods[index].deps[d], cap - 1);
                outName[cap - 1] = 0;
            }
            return TRUE;
        }
    }
    return FALSE;
}

static int ParseVersionSegments(const WCHAR* version, int* segments, int maxSegments) {
    int count = 0;
    if (!version || !segments || maxSegments <= 0) return 0;
    const WCHAR* p = version;
    if (*p == L'v' || *p == L'V') ++p;
    while (*p && count < maxSegments) {
        while (*p && (*p < L'0' || *p > L'9')) ++p;
        if (!*p) break;
        int value = 0;
        while (*p >= L'0' && *p <= L'9') {
            value = value * 10 + (*p - L'0');
            ++p;
        }
        segments[count++] = value;
    }
    return count;
}

static BOOL VersionIsLowerThan(const WCHAR* actual, const WCHAR* required) {
    int actualSegments[8] = {0};
    int requiredSegments[8] = {0};
    int actualCount = ParseVersionSegments(actual, actualSegments, 8);
    int requiredCount = ParseVersionSegments(required, requiredSegments, 8);
    if (actualCount == 0 || requiredCount == 0) return FALSE;
    int count = actualCount > requiredCount ? actualCount : requiredCount;
    for (int i = 0; i < count; ++i) {
        int a = i < actualCount ? actualSegments[i] : 0;
        int r = i < requiredCount ? requiredSegments[i] : 0;
        if (a < r) return TRUE;
        if (a > r) return FALSE;
    }
    return FALSE;
}

static BOOL HasTooLowDependencyVersion(int index, WCHAR* outName, int nameCap, WCHAR* outRequired, int requiredCap, WCHAR* outActual, int actualCap) {
    if (index < 0 || index >= g_modCount) return FALSE;
    for (int d = 0; d < g_mods[index].depCount; ++d) {
        if (!g_mods[index].depMinVersions[d][0]) continue;
        int dep = FindModIndexById(g_mods[index].deps[d]);
        if (dep < 0) continue;
        if (!VersionIsLowerThan(g_mods[dep].version, g_mods[index].depMinVersions[d])) continue;
        if (outName && nameCap > 0) {
            wcsncpy(outName, g_mods[index].deps[d], nameCap - 1);
            outName[nameCap - 1] = 0;
        }
        if (outRequired && requiredCap > 0) {
            wcsncpy(outRequired, g_mods[index].depMinVersions[d], requiredCap - 1);
            outRequired[requiredCap - 1] = 0;
        }
        if (outActual && actualCap > 0) {
            wcsncpy(outActual, g_mods[dep].version, actualCap - 1);
            outActual[actualCap - 1] = 0;
        }
        return TRUE;
    }
    return FALSE;
}

static BOOL HasTooLowGameVersion(int index, WCHAR* outRequired, int requiredCap, WCHAR* outActual, int actualCap) {
    if (index < 0 || index >= g_modCount) return FALSE;
    if (!g_mods[index].minGameVersion[0] || !g_gameVersion[0]) return FALSE;
    if (!VersionIsLowerThan(g_gameVersion, g_mods[index].minGameVersion)) return FALSE;
    if (outRequired && requiredCap > 0) {
        wcsncpy(outRequired, g_mods[index].minGameVersion, requiredCap - 1);
        outRequired[requiredCap - 1] = 0;
    }
    if (outActual && actualCap > 0) {
        wcsncpy(outActual, g_gameVersion, actualCap - 1);
        outActual[actualCap - 1] = 0;
    }
    return TRUE;
}

static void FindSteamUserRoot(WCHAR* out, int cap) {
    if (!out || cap <= 0) return;
    out[0] = 0;
    WCHAR roaming[MAX_PATH * 2];
    DWORD len = GetEnvironmentVariableW(L"APPDATA", roaming, _countof(roaming));
    if (len == 0 || len >= _countof(roaming)) return;
    WCHAR steamRoot[MAX_PATH * 2];
    swprintf(steamRoot, _countof(steamRoot), L"%ls\\SlayTheSpire2\\steam", roaming);
    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", steamRoot);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h == INVALID_HANDLE_VALUE) return;
    FILETIME newest = {0};
    do {
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) continue;
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
        BOOL digits = TRUE;
        for (int i = 0; fd.cFileName[i]; ++i) {
            if (fd.cFileName[i] < L'0' || fd.cFileName[i] > L'9') {
                digits = FALSE;
                break;
            }
        }
        if (!digits) continue;
        if (CompareFileTime(&fd.ftLastWriteTime, &newest) >= 0) {
            newest = fd.ftLastWriteTime;
            swprintf(out, cap, L"%ls\\%ls", steamRoot, fd.cFileName);
        }
    } while (FindNextFileW(h, &fd));
    FindClose(h);
}

static BOOL JsonObjectValueSpan(const char* json, const char* key, const char** outStart, const char** outEnd) {
    if (!json || !key || !outStart || !outEnd) return FALSE;
    char pattern[128];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);
    const char* p = strstr(JsonStart(json), pattern);
    if (!p) return FALSE;
    p = strchr(p + strlen(pattern), ':');
    if (!p) return FALSE;
    p = SkipWs(p + 1);
    if (*p != '{') return FALSE;
    const char* start = p;
    BOOL inString = FALSE, escaped = FALSE;
    int depth = 0;
    while (*p) {
        char c = *p;
        if (inString) {
            if (escaped) escaped = FALSE;
            else if (c == '\\') escaped = TRUE;
            else if (c == '"') inString = FALSE;
        } else {
            if (c == '"') inString = TRUE;
            else if (c == '{') ++depth;
            else if (c == '}') {
                --depth;
                if (depth == 0) {
                    *outStart = start;
                    *outEnd = p + 1;
                    return TRUE;
                }
            }
        }
        ++p;
    }
    return FALSE;
}

static BOOL JsonStringInObjectByWideKey(const char* objectStart, const char* objectEnd, const WCHAR* wideKey, WCHAR* out, int cap) {
    if (!objectStart || !objectEnd || !wideKey || !wideKey[0] || !out || cap <= 0) return FALSE;
    out[0] = 0;
    char key8[MAX_TEXT * 4];
    WideToUtf82(wideKey, key8, sizeof(key8));
    char pattern[MAX_TEXT * 4 + 4];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key8);
    const char* p = objectStart;
    while ((p = strstr(p, pattern)) != NULL && p < objectEnd) {
        const char* colon = strchr(p + strlen(pattern), ':');
        if (!colon || colon >= objectEnd) return FALSE;
        const char* value = SkipWs(colon + 1);
        if (value >= objectEnd || *value != '"') return FALSE;
        ++value;
        const char* start = value;
        while (*value && value < objectEnd && *value != '"') {
            if (*value == '\\' && value[1]) value += 2;
            else ++value;
        }
        if (value > start) {
            Utf8ToWide2(start, (int)(value - start), out, cap);
            return out[0] != 0;
        }
        p += strlen(pattern);
    }
    return FALSE;
}

static void ApplyBetterModMenuGroups(void) {
    WCHAR userRoot[MAX_PATH * 2];
    FindSteamUserRoot(userRoot, _countof(userRoot));
    if (!userRoot[0]) return;

    WCHAR path[MAX_PATH * 2];
    swprintf(path, _countof(path), L"%ls\\mod_data\\BetterModMenu\\mod_profiles.json", userRoot);
    DWORD size = 0;
    char* json = ReadFileBytes(path, &size);
    if (!json) return;

    const char* modGroupsStart = NULL;
    const char* modGroupsEnd = NULL;
    int imported = 0;
    if (JsonObjectValueSpan(json, "ModGroups", &modGroupsStart, &modGroupsEnd)) {
        for (int i = 0; i < g_modCount; ++i) {
            WCHAR group[MAX_TEXT] = L"";
            if (JsonStringInObjectByWideKey(modGroupsStart, modGroupsEnd, g_mods[i].id, group, _countof(group)) &&
                group[0] && _wcsicmp(group, L"Unassigned") != 0) {
                wcsncpy(g_mods[i].group, group, _countof(g_mods[i].group) - 1);
                g_mods[i].group[_countof(g_mods[i].group) - 1] = 0;
                ++imported;
            }
        }
    }
    if (imported > 0) AppendLogf(L"Imported Better Mod Menu groups=%d", imported);
    else AppendLogf(L"Better Mod Menu groups unavailable or empty: %ls", path);
    HeapFree(GetProcessHeap(), 0, json);
}

static BOOL NameOrIdContains(const ModInfo* mod, const WCHAR* text) {
    return mod && text && text[0] && (wcsstr(mod->id, text) || wcsstr(mod->name, text));
}

static void ApplyFallbackGroup(ModInfo* mod) {
    if (!mod || mod->group[0]) return;
    if (mod->depCount == 0 && (mod->orderBeforeCount > 0 || NameOrIdContains(mod, L"Lib") || NameOrIdContains(mod, L"lib"))) {
        wcscpy(mod->group, L"Dependencies / libraries");
    } else if (!mod->affectsGameplay && (NameOrIdContains(mod, L"Menu") || NameOrIdContains(mod, L"Config") || NameOrIdContains(mod, L"Restart") || NameOrIdContains(mod, L"Save") || NameOrIdContains(mod, L"Intent"))) {
        wcscpy(mod->group, L"UI / QoL");
    } else if (!mod->affectsGameplay && (NameOrIdContains(mod, L"Skin") || NameOrIdContains(mod, L"skin"))) {
        wcscpy(mod->group, L"Cosmetic");
    } else if (mod->affectsGameplay) {
        wcscpy(mod->group, L"Gameplay content");
    } else if (!mod->affectsGameplay) {
        wcscpy(mod->group, L"Utility / tools");
    } else {
        wcscpy(mod->group, L"Unknown");
    }
}

static void ApplyModGroups(void) {
    for (int i = 0; i < g_modCount; ++i) g_mods[i].group[0] = 0;
    ApplyBetterModMenuGroups();
    for (int i = 0; i < g_modCount; ++i) ApplyFallbackGroup(&g_mods[i]);
}

static int PruneInvalidChecks(void) {
    int pruned = 0;
    BOOL changed = TRUE;
    int guard = 0;
    while (changed && guard++ < MAX_DEPS + MAX_MODS) {
        changed = FALSE;
        for (int i = 0; i < g_modCount; ++i) {
            if (!ListView_GetCheckState(g_list, i)) continue;
            WCHAR depName[MAX_TEXT], required[MAX_TEXT], actual[MAX_TEXT];
            if (HasMissingDependency(i, depName, _countof(depName)) ||
                HasTooLowDependencyVersion(i, depName, _countof(depName), required, _countof(required), actual, _countof(actual)) ||
                HasTooLowGameVersion(i, required, _countof(required), actual, _countof(actual)) ||
                HasUncheckedKnownDependency(i, depName, _countof(depName))) {
                ListView_SetCheckState(g_list, i, FALSE);
                AppendLogf(L"Unchecked invalid saved/profile selection: %ls requires %ls", g_mods[i].id, depName);
                ++pruned;
                changed = TRUE;
            }
        }
    }
    return pruned;
}

static void BuildDependencyText(int index, WCHAR* out, int cap) {
    if (!out || cap <= 0) return;
    out[0] = 0;
    if (index < 0 || index >= g_modCount) return;
    for (int d = 0; d < g_mods[index].depCount; ++d) {
        if (d > 0) wcsncat(out, L", ", cap - wcslen(out) - 1);
        if (FindModIndexById(g_mods[index].deps[d]) < 0) {
            wcsncat(out, L"missing: ", cap - wcslen(out) - 1);
        }
        wcsncat(out, g_mods[index].deps[d], cap - wcslen(out) - 1);
        if (g_mods[index].depMinVersions[d][0]) {
            wcsncat(out, L" >= ", cap - wcslen(out) - 1);
            wcsncat(out, g_mods[index].depMinVersions[d], cap - wcslen(out) - 1);
        }
    }
    if (g_mods[index].minGameVersion[0]) {
        if (out[0]) wcsncat(out, L", ", cap - wcslen(out) - 1);
        wcsncat(out, L"requires STS2 >= ", cap - wcslen(out) - 1);
        wcsncat(out, g_mods[index].minGameVersion, cap - wcslen(out) - 1);
    }
    for (int d = 0; d < g_mods[index].orderAfterCount; ++d) {
        if (out[0]) wcsncat(out, L", ", cap - wcslen(out) - 1);
        wcsncat(out, L"loads after: ", cap - wcslen(out) - 1);
        wcsncat(out, g_mods[index].orderAfter[d], cap - wcslen(out) - 1);
    }
    for (int d = 0; d < g_mods[index].orderBeforeCount; ++d) {
        if (out[0]) wcsncat(out, L", ", cap - wcslen(out) - 1);
        wcsncat(out, L"loads before: ", cap - wcslen(out) - 1);
        wcsncat(out, g_mods[index].orderBefore[d], cap - wcslen(out) - 1);
    }
}

static void BuildStatusText(int index, WCHAR* out, int cap) {
    if (!out || cap <= 0) return;
    out[0] = 0;
    if (index < 0 || index >= g_modCount) return;
    WCHAR depName[MAX_TEXT];
    if (HasMissingDependency(index, depName, _countof(depName))) {
        swprintf(out, cap, L"Missing dependency: %ls", depName);
        return;
    }
    WCHAR required[MAX_TEXT], actual[MAX_TEXT];
    if (HasTooLowDependencyVersion(index, depName, _countof(depName), required, _countof(required), actual, _countof(actual))) {
        swprintf(out, cap, L"Dependency too old: %ls needs %ls, found %ls", depName, required, actual);
        return;
    }
    if (HasTooLowGameVersion(index, required, _countof(required), actual, _countof(actual))) {
        swprintf(out, cap, L"Game too old: needs %ls, found %ls", required, actual);
        return;
    }
    if (g_list && !ListView_GetCheckState(g_list, index)) {
        wcscpy(out, L"Available");
        return;
    }
    if (HasUncheckedKnownDependency(index, depName, _countof(depName))) {
        swprintf(out, cap, L"Select dependency: %ls", depName);
        return;
    }
    wcscpy(out, g_list && ListView_GetCheckState(g_list, index) ? L"Ready" : L"Available");
}

static void RefreshStatusColumn(void) {
    if (!g_list) return;
    for (int i = 0; i < g_modCount; ++i) {
        WCHAR statusText[MAX_TEXT] = L"";
        BuildStatusText(i, statusText, _countof(statusText));
        ListView_SetItemText(g_list, i, 7, statusText);
    }
}

static void UncheckDependentsRecursive(int dependencyIndex) {
    if (dependencyIndex < 0 || dependencyIndex >= g_modCount) return;
    for (int i = 0; i < g_modCount; ++i) {
        if (ListView_GetCheckState(g_list, i) && DependsOn(&g_mods[i], g_mods[dependencyIndex].id)) {
            ListView_SetCheckState(g_list, i, FALSE);
            UncheckDependentsRecursive(i);
        }
    }
}

static void EmitModTree(int index, int depth, BOOL* placed, ModInfo* sorted, int* out) {
    if (index < 0 || index >= g_modCount || placed[index]) return;
    ModInfo mod = g_mods[index];
    mod.depth = depth > 4 ? 4 : depth;
    sorted[(*out)++] = mod;
    placed[index] = TRUE;

    for (int i = 0; i < g_modCount; ++i) {
        if (!placed[i] && OrdersAfter(&g_mods[i], g_mods[index].id)) {
            EmitModTree(i, depth + 1, placed, sorted, out);
        }
    }
}

static void SortModsByDependencies(void) {
    CanonicalizeDependencyIds();
    if (g_savedOrderCount > 0) ApplySavedOrderBeforeDependencySort();
    else qsort(g_mods, g_modCount, sizeof(ModInfo), CmpMods);
    ModInfo* sorted = (ModInfo*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(ModInfo) * MAX_MODS);
    BOOL* placed = (BOOL*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(BOOL) * MAX_MODS);
    int out = 0;
    if (!sorted || !placed) {
        if (sorted) HeapFree(GetProcessHeap(), 0, sorted);
        if (placed) HeapFree(GetProcessHeap(), 0, placed);
        return;
    }

    for (int i = 0; i < g_modCount; ++i) {
        if (FirstKnownDepIndex(&g_mods[i]) < 0) {
            EmitModTree(i, 0, placed, sorted, &out);
        }
    }

    for (int i = 0; i < g_modCount; ++i) {
        if (!placed[i]) {
            EmitModTree(i, 0, placed, sorted, &out);
        }
    }

    for (int i = 0; i < g_modCount; ++i) {
        g_mods[i] = sorted[i];
    }

    HeapFree(GetProcessHeap(), 0, sorted);
    HeapFree(GetProcessHeap(), 0, placed);
}

static void SortModsByCurrentSettings(void) {
    CanonicalizeDependencyIds();
    if (g_savedOrderCount > 0) ApplySavedOrderBeforeDependencySort();
    else qsort(g_mods, g_modCount, sizeof(ModInfo), CmpMods);
    ApplyVisualDependencyIndentPreservingOrder();
}

static void BackupSettings(void) {
    WCHAR dir[MAX_PATH * 2], dst[MAX_PATH * 2], name[128];
    JoinPath(dir, _countof(dir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dir, NULL);
    wcscat(dir, L"\\settings-backups");
    CreateDirectoryW(dir, NULL);
    SYSTEMTIME st; GetLocalTime(&st);
    swprintf(name, _countof(name), L"settings.save.%04d%02d%02d-%02d%02d%02d.bak", st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
    JoinPath(dst, _countof(dst), dir, name);
    CopyFileW(g_settingsFile, dst, TRUE);
}

static const WCHAR* SourceForSettings(const ModInfo* mod) {
    return _wcsicmp(mod->source, L"Workshop") == 0 ? L"steam_workshop" : L"mods_directory";
}

static BOOL AppendBytes(char** out, DWORD* len, DWORD* cap, const char* bytes, DWORD bytesLen) {
    if (*len + bytesLen + 4 >= *cap) {
        while (*len + bytesLen + 4 >= *cap) *cap *= 2;
        char* bigger = (char*)HeapReAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, *out, *cap);
        if (!bigger) return FALSE;
        *out = bigger;
    }
    memcpy(*out + *len, bytes, bytesLen);
    *len += bytesLen;
    (*out)[*len] = 0;
    return TRUE;
}

static char* BuildModListJson(BOOL modded, const char* oldList, DWORD oldListLen, DWORD* outLen) {
    DWORD cap = 4096 + (DWORD)g_modCount * 1024 + oldListLen;
    char* out = (char*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, cap);
    if (!out) return NULL;
    DWORD len = 0;
    BOOL needComma = FALSE;
    out[len++] = '[';

    for (int i = 0; i < g_modCount; ++i) {
        char id8[MAX_TEXT * 4];
        char source8[128];
        WideToUtf82(g_mods[i].id, id8, sizeof(id8));
        WideToUtf82(SourceForSettings(&g_mods[i]), source8, sizeof(source8));
        BOOL on = modded && ListView_GetCheckState(g_list, i);
        char entry[MAX_TEXT * 4 + 256];
        snprintf(entry, sizeof(entry), "%s{\"id\":\"%s\",\"is_enabled\":%s,\"source\":\"%s\"}",
                 needComma ? "," : "", id8, on ? "true" : "false", source8);
        DWORD entryLen = (DWORD)strlen(entry);
        if (!AppendBytes(&out, &len, &cap, entry, entryLen)) {
            HeapFree(GetProcessHeap(), 0, out);
            return NULL;
        }
        needComma = TRUE;
    }

    const char* p = oldList;
    const char* end = oldList + oldListLen;
    while (p < end) {
        if (*p != '{') {
            ++p;
            continue;
        }
        const char* objectStart = p;
        BOOL inString = FALSE, escaped = FALSE;
        int depth = 0;
        while (p < end) {
            char c = *p;
            if (inString) {
                if (escaped) escaped = FALSE;
                else if (c == '\\') escaped = TRUE;
                else if (c == '"') inString = FALSE;
            } else {
                if (c == '"') inString = TRUE;
                else if (c == '{') ++depth;
                else if (c == '}') {
                    --depth;
                    if (depth == 0) {
                        ++p;
                        break;
                    }
                }
            }
            ++p;
        }
        const char* objectEnd = p;
        if (objectEnd > objectStart && !JsonObjectIdInDiscoveredMods(objectStart, objectEnd)) {
            if (needComma && !AppendBytes(&out, &len, &cap, ",", 1)) {
                HeapFree(GetProcessHeap(), 0, out);
                return NULL;
            }
            if (!AppendBytes(&out, &len, &cap, objectStart, (DWORD)(objectEnd - objectStart))) {
                HeapFree(GetProcessHeap(), 0, out);
                return NULL;
            }
            needComma = TRUE;
        }
    }

    out[len++] = ']';
    out[len] = 0;
    *outLen = len;
    return out;
}

static char* ReplaceModList(char* json, DWORD* size, BOOL modded) {
    DWORD start = 0, len = 0, newLen = 0;
    if (!FindModListSpan(json, *size, &start, &len)) return json;
    char* modList = BuildModListJson(modded, json + start, len, &newLen);
    if (!modList) return json;
    char* replaced = ReplaceSpan(json, size, start, len, modList);
    HeapFree(GetProcessHeap(), 0, modList);
    return replaced;
}

static char* ReplaceAll(const char* src, DWORD* size, const char* oldText, const char* newText) {
    const char* p = strstr(src, oldText);
    if (!p) return NULL;
    DWORD oldLen = (DWORD)strlen(oldText), newLen = (DWORD)strlen(newText), srcLen = *size;
    DWORD outLen = srcLen - oldLen + newLen;
    char* out = (char*)HeapAlloc(GetProcessHeap(), 0, outLen + 1);
    DWORD prefix = (DWORD)(p - src);
    memcpy(out, src, prefix);
    memcpy(out + prefix, newText, newLen);
    memcpy(out + prefix + newLen, p + oldLen, srcLen - prefix - oldLen);
    out[outLen] = 0;
    *size = outLen;
    return out;
}

static char* ReplaceSpan(char* src, DWORD* size, DWORD start, DWORD oldLen, const char* newText) {
    DWORD newLen = (DWORD)strlen(newText);
    DWORD outLen = *size - oldLen + newLen;
    char* out = (char*)HeapAlloc(GetProcessHeap(), 0, outLen + 1);
    if (!out) return src;
    memcpy(out, src, start);
    memcpy(out + start, newText, newLen);
    memcpy(out + start + newLen, src + start + oldLen, *size - start - oldLen);
    out[outLen] = 0;
    HeapFree(GetProcessHeap(), 0, src);
    *size = outLen;
    return out;
}

static char* InsertMissingModEntry(char* json, DWORD* size, const WCHAR* id, BOOL on) {
    char id8[MAX_TEXT * 4];
    WideToUtf82(id, id8, sizeof(id8));
    char entry[MAX_TEXT * 4 + 96];
    snprintf(entry, sizeof(entry), "{\"id\":\"%s\",\"is_enabled\":%s,\"source\":\"mods_directory\"},", id8, on ? "true" : "false");
    char* p = strstr(json, "\"mod_list\"");
    if (!p) return json;
    p = strchr(p, '[');
    if (!p) return json;
    return ReplaceSpan(json, size, (DWORD)(p + 1 - json), 0, entry);
}

static void WriteSettings(BOOL modded) {
    if (!FileExistsW2(g_settingsFile)) {
        MessageBoxW(NULL, L"settings.save was not found. Start the game once first, or check the path.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    BackupSettings();
    DWORD size = 0;
    char* json = ReadFileBytes(g_settingsFile, &size);
    if (!json) return;
    char* replaced = NULL;
    if (modded) replaced = ReplaceAll(json, &size, "\"mods_enabled\": false", "\"mods_enabled\": true");
    else replaced = ReplaceAll(json, &size, "\"mods_enabled\": true", "\"mods_enabled\": false");
    if (replaced) { HeapFree(GetProcessHeap(), 0, json); json = replaced; }

    if (modded) {
        json = ReplaceModList(json, &size, TRUE);
    } else {
        AppendLog(L"Vanilla launch: preserving mod_list order and per-mod enabled states");
    }
    WriteFileBytes(g_settingsFile, json, size);
    HeapFree(GetProcessHeap(), 0, json);
}

static void RefreshList(void) {
    ListView_DeleteAllItems(g_list);
    g_modCount = 0;
    g_savedEnabledCount = 0;
    LoadGameVersion();
    DWORD size = 0;
    char* settings = ReadFileBytes(g_settingsFile, &size);
    if (settings) {
        LoadSettingsOrderFromBytes(settings, size);
    } else {
        g_savedOrderCount = 0;
    }
    WCHAR root[MAX_PATH * 2], workshop[MAX_PATH * 2];
    swprintf(root, _countof(root), L"%ls\\mods", g_gameDir);
    AppendLogf(L"Refresh appDir=%ls", g_appDir);
    AppendLogf(L"Refresh gameDir=%ls", g_gameDir);
    AppendLogf(L"Refresh settings=%ls", g_settingsFile);
    DiscoverRoot(root, L"Local");
    FindWorkshopDir(workshop, _countof(workshop));
    DiscoverRoot(workshop, L"Workshop");
    SortModsByCurrentSettings();
    ApplyModGroups();
    AppendLogf(L"Refresh total mods=%d", g_modCount);
    g_enforcingChecks = TRUE;
    for (int i = 0; i < g_modCount; ++i) {
        WCHAR displayName[MAX_TEXT + 32];
        const WCHAR* indent = g_mods[i].depth == 0 ? L"" : (g_mods[i].depth == 1 ? L"    \u2514 " : L"        \u2514 ");
        swprintf(displayName, _countof(displayName), L"%ls%ls", indent, g_mods[i].name);
        LVITEMW item = {0};
        item.mask = LVIF_TEXT;
        item.iItem = i;
        item.pszText = displayName;
        ListView_InsertItem(g_list, &item);
        ListView_SetItemText(g_list, i, 1, g_mods[i].id);
        ListView_SetItemText(g_list, i, 2, g_mods[i].source);
        WCHAR typeText[32];
        wcsncpy(typeText, g_mods[i].affectsGameplay ? L"Gameplay" : L"Utility", _countof(typeText) - 1);
        typeText[_countof(typeText) - 1] = 0;
        ListView_SetItemText(g_list, i, 3, typeText);
        WCHAR orderText[32];
        swprintf(orderText, _countof(orderText), L"%d", i + 1);
        ListView_SetItemText(g_list, i, 4, g_mods[i].group);
        ListView_SetItemText(g_list, i, 5, orderText);
        WCHAR depText[MAX_TEXT] = L"";
        BuildDependencyText(i, depText, _countof(depText));
        ListView_SetItemText(g_list, i, 6, depText);
        if (settings && JsonBoolAfterId(settings, g_mods[i].id)) {
            ListView_SetCheckState(g_list, i, TRUE);
        }
        WCHAR statusText[MAX_TEXT] = L"";
        BuildStatusText(i, statusText, _countof(statusText));
        ListView_SetItemText(g_list, i, 7, statusText);
    }
    int pruned = PruneInvalidChecks();
    RefreshStatusColumn();
    g_enforcingChecks = FALSE;
    if (settings) HeapFree(GetProcessHeap(), 0, settings);
    WCHAR status[MAX_PATH * 2];
    if (g_modCount == 0) {
        swprintf(status, _countof(status), L"Found 0 mods. Game: %ls", g_gameDir);
    } else if (pruned > 0) {
        swprintf(status, _countof(status), L"Found %d mods. Removed %d invalid saved selection(s).", g_modCount, pruned);
    } else {
        swprintf(status, _countof(status), L"Found %d mods. Showing current game settings.", g_modCount);
    }
    SetWindowTextW(g_status, status);
}

static void QuoteArg(WCHAR* out, int cap, const WCHAR* arg) {
    if (!wcspbrk(arg, L" \t\"")) {
        wcsncpy(out, arg, cap - 1);
        out[cap - 1] = 0;
        return;
    }
    swprintf(out, cap, L"\"%ls\"", arg);
}

static int GetSelectedListIndex(void) {
    return ListView_GetNextItem(g_list, -1, LVNI_SELECTED);
}

static void SelectListIndex(int index) {
    if (index < 0 || index >= g_modCount) return;
    ListView_SetItemState(g_list, -1, 0, LVIS_SELECTED | LVIS_FOCUSED);
    ListView_SetItemState(g_list, index, LVIS_SELECTED | LVIS_FOCUSED, LVIS_SELECTED | LVIS_FOCUSED);
    ListView_EnsureVisible(g_list, index, FALSE);
}

static void RebuildListPreservingChecks(int selectedIndex) {
    WCHAR (*checked)[MAX_TEXT] = (WCHAR (*)[MAX_TEXT])HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(WCHAR) * MAX_MODS * MAX_TEXT);
    if (!checked) {
        SetWindowTextW(g_status, L"Could not rebuild list: out of memory.");
        return;
    }
    int checkedCount = 0;
    for (int i = 0; i < g_modCount && checkedCount < MAX_MODS; ++i) {
        if (ListView_GetCheckState(g_list, i)) {
            WCHAR id[MAX_TEXT] = L"";
            ListView_GetItemText(g_list, i, 1, id, _countof(id));
            if (!id[0]) {
                wcsncpy(id, g_mods[i].id, _countof(id) - 1);
                id[_countof(id) - 1] = 0;
            }
            wcsncpy(checked[checkedCount], id, MAX_TEXT - 1);
            checked[checkedCount][MAX_TEXT - 1] = 0;
            ++checkedCount;
        }
    }

    ListView_DeleteAllItems(g_list);
    g_enforcingChecks = TRUE;
    for (int i = 0; i < g_modCount; ++i) {
        WCHAR displayName[MAX_TEXT + 32];
        const WCHAR* indent = g_mods[i].depth == 0 ? L"" : (g_mods[i].depth == 1 ? L"    \x2514 " : L"        \x2514 ");
        swprintf(displayName, _countof(displayName), L"%ls%ls", indent, g_mods[i].name);
        LVITEMW item = {0};
        item.mask = LVIF_TEXT;
        item.iItem = i;
        item.pszText = displayName;
        ListView_InsertItem(g_list, &item);
        ListView_SetItemText(g_list, i, 1, g_mods[i].id);
        ListView_SetItemText(g_list, i, 2, g_mods[i].source);
        ListView_SetItemText(g_list, i, 3, g_mods[i].affectsGameplay ? L"Gameplay" : L"Utility");
        WCHAR orderText[32];
        swprintf(orderText, _countof(orderText), L"%d", i + 1);
        ListView_SetItemText(g_list, i, 4, g_mods[i].group);
        ListView_SetItemText(g_list, i, 5, orderText);
        WCHAR depText[MAX_TEXT] = L"";
        BuildDependencyText(i, depText, _countof(depText));
        ListView_SetItemText(g_list, i, 6, depText);
        for (int c = 0; c < checkedCount; ++c) {
            if (_wcsicmp(checked[c], g_mods[i].id) == 0) {
                ListView_SetCheckState(g_list, i, TRUE);
                break;
            }
        }
        WCHAR statusText[MAX_TEXT] = L"";
        BuildStatusText(i, statusText, _countof(statusText));
        ListView_SetItemText(g_list, i, 7, statusText);
    }
    g_enforcingChecks = FALSE;
    RefreshStatusColumn();
    SelectListIndex(selectedIndex);
    HeapFree(GetProcessHeap(), 0, checked);
}

static void MoveSelectedMod(int delta) {
    int index = GetSelectedListIndex();
    int target = index + delta;
    if (index < 0 || target < 0 || target >= g_modCount) return;

    if (delta > 0 && DependsOn(&g_mods[index], g_mods[target].id)) {
        SetWindowTextW(g_status, L"Cannot move a dependency below a mod that needs it.");
        return;
    }
    if (delta < 0 && DependsOn(&g_mods[target], g_mods[index].id)) {
        SetWindowTextW(g_status, L"Cannot move a mod above its dependency.");
        return;
    }

    ModInfo tmp = g_mods[index];
    g_mods[index] = g_mods[target];
    g_mods[target] = tmp;
    RebuildListPreservingChecks(target);
    SetWindowTextW(g_status, L"Order changed. Choose or type a profile name, then Save.");
}

static BOOL TryMoveIndexForTest(int index, int delta) {
    int target = index + delta;
    if (index < 0 || index >= g_modCount || target < 0 || target >= g_modCount) return FALSE;
    if (delta > 0 && DependsOn(&g_mods[index], g_mods[target].id)) return FALSE;
    if (delta < 0 && DependsOn(&g_mods[target], g_mods[index].id)) return FALSE;
    ModInfo tmp = g_mods[index];
    g_mods[index] = g_mods[target];
    g_mods[target] = tmp;
    return TRUE;
}

static BOOL MoveModToIndex(int index, int target) {
    if (index < 0 || index >= g_modCount || target < 0 || target >= g_modCount) return FALSE;
    if (target == index) return TRUE;
    ModInfo moving = g_mods[index];
    if (target < index) {
        for (int i = index; i > target; --i) g_mods[i] = g_mods[i - 1];
    } else {
        for (int i = index; i < target; ++i) g_mods[i] = g_mods[i + 1];
    }
    g_mods[target] = moving;
    return TRUE;
}

static BOOL ApplyNumericOrderForIndex(int index, int target, int* outSelected, BOOL* outRepaired) {
    if (outSelected) *outSelected = target;
    if (outRepaired) *outRepaired = FALSE;
    if (index < 0 || index >= g_modCount || target < 0 || target >= g_modCount) return FALSE;
    WCHAR movingId[MAX_TEXT];
    wcsncpy(movingId, g_mods[index].id, _countof(movingId) - 1);
    movingId[_countof(movingId) - 1] = 0;
    if (!MoveModToIndex(index, target)) return FALSE;
    BOOL repaired = RepairDependencyOrderInPlace();
    int selected = FindModIndexById(movingId);
    if (outSelected) *outSelected = selected >= 0 ? selected : target;
    if (outRepaired) *outRepaired = repaired;
    return TRUE;
}

static BOOL RunProfileRepairSelfTest(void) {
    int base = FindModIndexById(L"BaseLib");
    int quick = FindModIndexById(L"QuickRestart");
    if (base < 0 || quick < 0) {
        AppendLog(L"Profile repair self-test skipped: BaseLib/QuickRestart not present");
        return TRUE;
    }

    WCHAR profilePath[MAX_PATH * 2];
    GetProfilePath(L"profile-repair-self-test", profilePath, _countof(profilePath));
    HANDLE h = CreateFileW(profilePath, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        AppendLog(L"Profile repair self-test failed: could not create profile");
        return FALSE;
    }

    const WCHAR* ids[] = {
        L"QuickRestart",
        L"MissingProfileMod",
        L"BaseLib"
    };
    for (int i = 0; i < (int)(sizeof(ids) / sizeof(ids[0])); ++i) {
        char id8[MAX_TEXT * 4];
        int len = WideToUtf82(ids[i], id8, sizeof(id8));
        if (len > 1) {
            DWORD written = 0;
            WriteFile(h, id8, (DWORD)(len - 1), &written, NULL);
            WriteFile(h, "\r\n", 2, &written, NULL);
        }
    }
    CloseHandle(h);

    BOOL repaired = LoadOrderFromPathCore(profilePath, FALSE);
    DeleteFileW(profilePath);
    if (!repaired) {
        AppendLog(L"Profile repair self-test failed: profile did not report dependency adjustment");
        return FALSE;
    }

    WCHAR message[MAX_TEXT * 2];
    if (!ValidateDependencyOrder(message, _countof(message))) {
        AppendLogf(L"Profile repair self-test failed: %ls", message);
        return FALSE;
    }
    if (FindModIndexById(L"MissingProfileMod") >= 0) {
        AppendLog(L"Profile repair self-test failed: missing mod appeared in loaded order");
        return FALSE;
    }
    base = FindModIndexById(L"BaseLib");
    quick = FindModIndexById(L"QuickRestart");
    if (base < 0 || quick < 0 || base > quick) {
        AppendLog(L"Profile repair self-test failed: BaseLib not before QuickRestart after repair");
        return FALSE;
    }

    AppendLog(L"Profile repair self-test passed");
    return TRUE;
}

static BOOL BackupExistsForPath(const WCHAR* path) {
    WCHAR dir[MAX_PATH * 2], stem[MAX_PROFILE + 64], search[MAX_PATH * 2];
    GetFileBackupsDir(dir, _countof(dir));
    SafeFileStem(path, stem, _countof(stem));
    swprintf(search, _countof(search), L"%ls\\%ls.*.bak", dir, stem);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h == INVALID_HANDLE_VALUE) return FALSE;
    FindClose(h);
    return TRUE;
}

static BOOL RunDataFileBackupSelfTest(void) {
    WCHAR profilePath[MAX_PATH * 2];
    GetProfilePath(L"self-test-backup", profilePath, _countof(profilePath));
    DeleteFileW(profilePath);
    if (!SaveOrderToPath(profilePath)) {
        AppendLog(L"Data-file backup self-test failed: initial save failed");
        return FALSE;
    }
    if (!SaveOrderToPath(profilePath)) {
        AppendLog(L"Data-file backup self-test failed: overwrite save failed");
        DeleteFileW(profilePath);
        return FALSE;
    }
    if (!BackupExistsForPath(profilePath)) {
        AppendLog(L"Data-file backup self-test failed: backup was not created");
        DeleteFileW(profilePath);
        return FALSE;
    }
    DeleteFileW(profilePath);
    AppendLog(L"Data-file backup self-test passed");
    return TRUE;
}

static BOOL FileContainsUtf8Token(const WCHAR* path, const WCHAR* token) {
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) return FALSE;
    char token8[MAX_TEXT * 4];
    WideToUtf82(token, token8, sizeof(token8));
    BOOL found = strstr(data, token8) != NULL;
    HeapFree(GetProcessHeap(), 0, data);
    return found;
}

static char* ReadModListSpanCopy(const WCHAR* path, DWORD* outLen) {
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) return NULL;
    DWORD start = 0, len = 0;
    if (!FindModListSpan(data, size, &start, &len)) {
        HeapFree(GetProcessHeap(), 0, data);
        return NULL;
    }
    char* copy = (char*)HeapAlloc(GetProcessHeap(), 0, len + 1);
    if (!copy) {
        HeapFree(GetProcessHeap(), 0, data);
        return NULL;
    }
    memcpy(copy, data + start, len);
    copy[len] = 0;
    if (outLen) *outLen = len;
    HeapFree(GetProcessHeap(), 0, data);
    return copy;
}

static BOOL IsSelfTestSelectableAlone(int index) {
    if (index < 0 || index >= g_modCount) return FALSE;
    WCHAR depName[MAX_TEXT], required[MAX_TEXT], actual[MAX_TEXT];
    if (HasMissingDependency(index, depName, _countof(depName))) return FALSE;
    if (HasTooLowDependencyVersion(index, depName, _countof(depName), required, _countof(required), actual, _countof(actual))) return FALSE;
    if (HasTooLowGameVersion(index, required, _countof(required), actual, _countof(actual))) return FALSE;
    return g_mods[index].depCount == 0;
}

static BOOL RunNamedProfileSaveSelfTest(void) {
    if (!g_list || g_modCount < 2) return TRUE;
    int first = -1, second = -1;
    for (int i = 0; i < g_modCount; ++i) {
        if (!IsSelfTestSelectableAlone(i)) continue;
        if (first < 0) first = i;
        else {
            second = i;
            break;
        }
    }
    if (first < 0 || second < 0) {
        AppendLog(L"Named profile self-test skipped: not enough standalone selectable mods");
        return TRUE;
    }

    const WCHAR* profileName = L"self-test-profile-save-update";
    WCHAR profilePath[MAX_PATH * 2], enabledPath[MAX_PATH * 2];
    GetProfilePath(profileName, profilePath, _countof(profilePath));
    GetProfileEnabledPath(profileName, enabledPath, _countof(enabledPath));
    DeleteFileW(profilePath);
    DeleteFileW(enabledPath);

    HWND oldCombo = g_profileCombo;
    if (!g_profileCombo) {
        g_profileCombo = CreateWindowW(WC_COMBOBOXW, L"", WS_CHILD | CBS_DROPDOWN, 0, 0, 120, 80, GetParent(g_list), NULL, g_instance, NULL);
    }
    if (!g_profileCombo) {
        AppendLog(L"Named profile self-test failed: could not create profile combo");
        return FALSE;
    }

    for (int i = 0; i < g_modCount; ++i) ListView_SetCheckState(g_list, i, FALSE);
    ListView_SetCheckState(g_list, first, TRUE);
    SetWindowTextW(g_profileCombo, profileName);
    SaveNamedProfile();
    if (!FileExistsW2(profilePath) || !FileExistsW2(enabledPath)) {
        AppendLog(L"Named profile self-test failed: profile files were not created");
        if (!oldCombo && g_profileCombo) DestroyWindow(g_profileCombo);
        g_profileCombo = oldCombo;
        return FALSE;
    }
    if (!FileContainsUtf8Token(enabledPath, g_mods[first].id) || FileContainsUtf8Token(enabledPath, g_mods[second].id)) {
        AppendLog(L"Named profile self-test failed: initial enabled selection was not saved exactly");
        if (!oldCombo && g_profileCombo) DestroyWindow(g_profileCombo);
        g_profileCombo = oldCombo;
        DeleteFileW(profilePath);
        DeleteFileW(enabledPath);
        return FALSE;
    }

    for (int i = 0; i < g_modCount; ++i) ListView_SetCheckState(g_list, i, FALSE);
    ListView_SetCheckState(g_list, second, TRUE);
    SetWindowTextW(g_profileCombo, profileName);
    SaveNamedProfile();
    if (FileContainsUtf8Token(enabledPath, g_mods[first].id) || !FileContainsUtf8Token(enabledPath, g_mods[second].id)) {
        AppendLog(L"Named profile self-test failed: same-name Save did not update enabled selections");
        if (!oldCombo && g_profileCombo) DestroyWindow(g_profileCombo);
        g_profileCombo = oldCombo;
        DeleteFileW(profilePath);
        DeleteFileW(enabledPath);
        return FALSE;
    }

    for (int i = 0; i < g_modCount; ++i) ListView_SetCheckState(g_list, i, FALSE);
    if (!LoadEnabledFromPathToList(enabledPath)) {
        AppendLog(L"Named profile self-test failed: updated profile enabled file did not load");
        if (!oldCombo && g_profileCombo) DestroyWindow(g_profileCombo);
        g_profileCombo = oldCombo;
        DeleteFileW(profilePath);
        DeleteFileW(enabledPath);
        return FALSE;
    }
    if (ListView_GetCheckState(g_list, first) || !ListView_GetCheckState(g_list, second)) {
        AppendLog(L"Named profile self-test failed: loaded updated profile did not restore expected checks");
        if (!oldCombo && g_profileCombo) DestroyWindow(g_profileCombo);
        g_profileCombo = oldCombo;
        DeleteFileW(profilePath);
        DeleteFileW(enabledPath);
        return FALSE;
    }

    if (!oldCombo && g_profileCombo) DestroyWindow(g_profileCombo);
    g_profileCombo = oldCombo;
    DeleteFileW(profilePath);
    DeleteFileW(enabledPath);
    AppendLog(L"Named profile self-test passed");
    return TRUE;
}

static BOOL RunSelectedOnlyDependencyValidationSelfTest(void) {
    if (!g_list) return TRUE;
    int base = FindModIndexById(L"BaseLib");
    int quick = FindModIndexById(L"QuickRestart");
    if (base < 0 || quick < 0) {
        AppendLog(L"Selected-only dependency validation self-test skipped: BaseLib/QuickRestart not present");
        return TRUE;
    }

    ModInfo* snapshot = (ModInfo*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(ModInfo) * MAX_MODS);
    if (!snapshot) {
        AppendLog(L"Selected-only dependency validation self-test failed: out of memory");
        return FALSE;
    }
    for (int i = 0; i < g_modCount; ++i) snapshot[i] = g_mods[i];

    if (base < quick) {
        MoveModToIndex(base, quick);
    } else if (quick < base) {
        MoveModToIndex(quick, base);
    }
    base = FindModIndexById(L"BaseLib");
    quick = FindModIndexById(L"QuickRestart");
    if (base < 0 || quick < 0 || base < quick) {
        for (int i = 0; i < g_modCount; ++i) g_mods[i] = snapshot[i];
        HeapFree(GetProcessHeap(), 0, snapshot);
        AppendLog(L"Selected-only dependency validation self-test skipped: could not create invalid disabled order");
        return TRUE;
    }

    RebuildListPreservingChecks(quick);
    for (int i = 0; i < g_modCount; ++i) ListView_SetCheckState(g_list, i, FALSE);
    WCHAR message[MAX_TEXT * 2];
    BOOL allOk = ValidateDependencyOrder(message, _countof(message));
    BOOL selectedOk = ValidateSelectedDependencyOrder(message, _countof(message));

    for (int i = 0; i < g_modCount; ++i) g_mods[i] = snapshot[i];
    HeapFree(GetProcessHeap(), 0, snapshot);
    RebuildListPreservingChecks(0);

    if (allOk) {
        AppendLog(L"Selected-only dependency validation self-test failed: full validation did not detect invalid disabled order");
        return FALSE;
    }
    if (!selectedOk) {
        AppendLogf(L"Selected-only dependency validation self-test failed: selected-only validation blocked disabled mods: %ls", message);
        return FALSE;
    }
    AppendLog(L"Selected-only dependency validation self-test passed");
    return TRUE;
}

static BOOL RunHiddenListMoveTest(void) {
    INITCOMMONCONTROLSEX icc = { sizeof(icc), ICC_LISTVIEW_CLASSES };
    InitCommonControlsEx(&icc);

    HWND host = CreateWindowW(L"STATIC", L"", WS_OVERLAPPED, 0, 0, 100, 100, NULL, NULL, g_instance, NULL);
    if (!host) {
        AppendLog(L"Order self-test failed: could not create hidden host window");
        return FALSE;
    }

    HWND oldList = g_list;
    HWND oldStatus = g_status;
    g_list = CreateWindowW(WC_LISTVIEWW, L"", WS_CHILD | LVS_REPORT | LVS_SINGLESEL, 0, 0, 100, 100, host, NULL, g_instance, NULL);
    g_status = CreateWindowW(L"STATIC", L"", WS_CHILD, 0, 0, 100, 20, host, NULL, g_instance, NULL);
    if (!g_list || !g_status) {
        AppendLog(L"Order self-test failed: could not create hidden list controls");
        if (g_list) DestroyWindow(g_list);
        if (g_status) DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }
    ListView_SetExtendedListViewStyle(g_list, LVS_EX_CHECKBOXES | LVS_EX_FULLROWSELECT);

    LVCOLUMNW col = {0};
    col.mask = LVCF_TEXT | LVCF_WIDTH;
    col.cx = 100; col.pszText = (LPWSTR)L"Name"; ListView_InsertColumn(g_list, 0, &col);
    col.cx = 100; col.pszText = (LPWSTR)L"ID"; ListView_InsertColumn(g_list, 1, &col);
    col.cx = 80; col.pszText = (LPWSTR)L"Source"; ListView_InsertColumn(g_list, 2, &col);
    col.cx = 80; col.pszText = (LPWSTR)L"Type"; ListView_InsertColumn(g_list, 3, &col);
    col.cx = 100; col.pszText = (LPWSTR)L"Group"; ListView_InsertColumn(g_list, 4, &col);
    col.cx = 60; col.pszText = (LPWSTR)L"Order"; ListView_InsertColumn(g_list, 5, &col);
    col.cx = 100; col.pszText = (LPWSTR)L"Requirements"; ListView_InsertColumn(g_list, 6, &col);
    col.cx = 120; col.pszText = (LPWSTR)L"Status"; ListView_InsertColumn(g_list, 7, &col);

    RebuildListPreservingChecks(0);
    int firstCount = ListView_GetItemCount(g_list);
    if (firstCount != g_modCount) {
        AppendLogf(L"Order self-test failed: hidden list initial count=%d expected=%d", firstCount, g_modCount);
        DestroyWindow(g_list);
        DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }

    WCHAR firstId[MAX_TEXT], lastId[MAX_TEXT];
    wcscpy(firstId, g_mods[0].id);
    wcscpy(lastId, g_mods[g_modCount - 1].id);
    SelectListIndex(0);
    MoveSelectedMod(-1);
    SelectListIndex(g_modCount - 1);
    MoveSelectedMod(1);
    if (_wcsicmp(firstId, g_mods[0].id) != 0 || _wcsicmp(lastId, g_mods[g_modCount - 1].id) != 0) {
        AppendLog(L"Order self-test failed: hidden list boundary button path changed order");
        DestroyWindow(g_list);
        DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }

    int missingDep = -1;
    for (int i = 0; i < g_modCount; ++i) {
        if (HasMissingDependency(i, NULL, 0)) {
            missingDep = i;
            break;
        }
    }
    if (missingDep >= 0) {
        for (int i = 0; i < g_modCount; ++i) {
            ListView_SetCheckState(g_list, i, FALSE);
        }
        ListView_SetCheckState(g_list, missingDep, TRUE);
        int pruned = PruneInvalidChecks();
        if (pruned <= 0 || ListView_GetCheckState(g_list, missingDep)) {
            AppendLog(L"Order self-test failed: missing dependency selection was not pruned");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        AppendLog(L"Order self-test pruned missing dependency selection");
    }

    int independent = -1;
    for (int i = 1; i < g_modCount - 1; ++i) {
        if (g_mods[i].depCount == 0) {
            independent = i;
            break;
        }
    }
    if (independent >= 0) {
        WCHAR movingId[MAX_TEXT];
        wcscpy(movingId, g_mods[independent].id);
        for (int i = 0; i < g_modCount; ++i) {
            ListView_SetCheckState(g_list, i, FALSE);
        }
        ListView_SetCheckState(g_list, independent, TRUE);
        SelectListIndex(independent);
        MoveSelectedMod(-1);
        int moved = FindModIndexById(movingId);
        if (moved < 0 || moved >= independent || ListView_GetItemCount(g_list) != g_modCount) {
            AppendLog(L"Order self-test failed: hidden list independent Move Up path failed");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        if (!ListView_GetCheckState(g_list, moved)) {
            AppendLog(L"Order self-test failed: hidden list checked state did not follow moved mod");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        if (!SaveCurrentEnabled()) {
            AppendLog(L"Order self-test failed: could not save enabled-mods.txt");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        WCHAR enabledPath[MAX_PATH * 2];
        GetEnabledModsPath(enabledPath, _countof(enabledPath));
        DWORD enabledSize = 0;
        char* enabledData = ReadFileBytes(enabledPath, &enabledSize);
        char movingId8[MAX_TEXT * 4];
        WideToUtf82(movingId, movingId8, sizeof(movingId8));
        if (!enabledData || !strstr(enabledData, movingId8)) {
            AppendLog(L"Order self-test failed: enabled-mods.txt did not include moved checked mod");
            if (enabledData) HeapFree(GetProcessHeap(), 0, enabledData);
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        HeapFree(GetProcessHeap(), 0, enabledData);

        WCHAR profileEnabledPath[MAX_PATH * 2];
        GetProfileEnabledPath(L"self-test-enabled", profileEnabledPath, _countof(profileEnabledPath));
        if (!SaveCurrentEnabledToPath(profileEnabledPath) || !FileExistsW2(profileEnabledPath)) {
            AppendLog(L"Order self-test failed: profile enabled selections were not saved");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        for (int i = 0; i < g_modCount; ++i) {
            ListView_SetCheckState(g_list, i, FALSE);
        }
        if (!LoadEnabledFromPathToList(profileEnabledPath)) {
            AppendLog(L"Order self-test failed: profile enabled selections were not loaded");
            DeleteFileW(profileEnabledPath);
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        DeleteFileW(profileEnabledPath);
        moved = FindModIndexById(movingId);
        if (moved < 0 || !ListView_GetCheckState(g_list, moved)) {
            AppendLog(L"Order self-test failed: loaded profile enabled selections did not restore checked mod");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
    }

    if (!RunNamedProfileSaveSelfTest()) {
        DestroyWindow(g_list);
        DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }

    if (!RunSelectedOnlyDependencyValidationSelfTest()) {
        DestroyWindow(g_list);
        DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }

    DestroyWindow(g_list);
    DestroyWindow(g_status);
    DestroyWindow(host);
    g_list = oldList;
    g_status = oldStatus;
    return TRUE;
}

static void ApplySelectedOrderNumber(void) {
    int index = GetSelectedListIndex();
    if (index < 0 || index >= g_modCount) {
        SetWindowTextW(g_status, L"Select a mod before applying an order number.");
        return;
    }
    WCHAR text[64];
    GetWindowTextW(g_orderEdit, text, _countof(text));
    WCHAR* end = NULL;
    long value = wcstol(text, &end, 10);
    if (value < 1) value = 1;
    if (value > g_modCount) value = g_modCount;
    int target = (int)value - 1;
    if (target == index) {
        SetWindowTextW(g_status, L"Order number unchanged.");
        return;
    }

    int selected = target;
    BOOL repaired = FALSE;
    ApplyNumericOrderForIndex(index, target, &selected, &repaired);
    RebuildListPreservingChecks(selected);
    SetWindowTextW(g_status, repaired
        ? L"Order number applied. Order was adjusted to satisfy dependencies."
        : L"Order number applied. Choose or type a profile name, then Save.");
}

static void RefreshProfiles(void) {
    if (!g_profileCombo) return;
    g_refreshingProfiles = TRUE;
    SendMessageW(g_profileCombo, CB_RESETCONTENT, 0, 0);
    SendMessageW(g_profileCombo, CB_ADDSTRING, 0, (LPARAM)CURRENT_PROFILE_LABEL);
    WCHAR dir[MAX_PATH * 2], search[MAX_PATH * 2];
    GetProfilesDir(dir, _countof(dir));
    swprintf(search, _countof(search), L"%ls\\*.txt", dir);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h != INVALID_HANDLE_VALUE) {
        do {
            if (wcsstr(fd.cFileName, L".enabled.txt")) {
                continue;
            }
            WCHAR name[MAX_PROFILE];
            wcsncpy(name, fd.cFileName, _countof(name) - 1);
            name[_countof(name) - 1] = 0;
            WCHAR* dot = wcsrchr(name, L'.');
            if (dot) *dot = 0;
            if (_wcsicmp(name, CURRENT_PROFILE_LABEL) == 0) continue;
            if (name[0]) SendMessageW(g_profileCombo, CB_ADDSTRING, 0, (LPARAM)name);
        } while (FindNextFileW(h, &fd));
        FindClose(h);
    }
    SendMessageW(g_profileCombo, CB_SETCURSEL, 0, 0);
    g_refreshingProfiles = FALSE;
}

static void SelectProfileByName(const WCHAR* profile) {
    if (!g_profileCombo || !profile || !profile[0]) return;
    int count = (int)SendMessageW(g_profileCombo, CB_GETCOUNT, 0, 0);
    for (int i = 0; i < count; ++i) {
        WCHAR item[MAX_PROFILE];
        item[0] = 0;
        SendMessageW(g_profileCombo, CB_GETLBTEXT, i, (LPARAM)item);
        if (_wcsicmp(item, profile) == 0) {
            SendMessageW(g_profileCombo, CB_SETCURSEL, i, 0);
            return;
        }
    }
    SetWindowTextW(g_profileCombo, profile);
}

static void GetCurrentProfileName(WCHAR* out, int cap) {
    GetWindowTextW(g_profileCombo, out, cap);
    if (!out[0]) wcsncpy(out, CURRENT_PROFILE_LABEL, cap - 1);
    out[cap - 1] = 0;
}

static BOOL IsCurrentSettingsProfile(const WCHAR* profile) {
    return !profile || !profile[0] || _wcsicmp(profile, CURRENT_PROFILE_LABEL) == 0;
}

static void SaveNamedProfile(void) {
    WCHAR profile[MAX_PROFILE], path[MAX_PATH * 2], enabledPath[MAX_PATH * 2];
    GetCurrentProfileName(profile, _countof(profile));
    if (IsCurrentSettingsProfile(profile)) {
        MessageBoxW(NULL, L"Type a profile name before saving. Current settings.save is the live game settings view and is not a named profile.", L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        return;
    }
    GetProfilePath(profile, path, _countof(path));
    if (!SaveOrderToPath(path)) {
        MessageBoxW(NULL, L"Could not save profile.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    GetProfileEnabledPath(profile, enabledPath, _countof(enabledPath));
    if (!SaveCurrentEnabledToPath(enabledPath)) {
        MessageBoxW(NULL, L"Could not save profile enabled selections.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    RefreshProfiles();
    SelectProfileByName(profile);
    SetWindowTextW(g_status, L"Profile saved with current order and enabled selections.");
}

static BOOL LoadOrderFromPathCore(const WCHAR* path, BOOL rebuildList) {
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) {
        if (g_status) SetWindowTextW(g_status, L"Profile was not found.");
        return FALSE;
    }
    WCHAR (*ids)[MAX_TEXT] = (WCHAR (*)[MAX_TEXT])HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(WCHAR) * MAX_MODS * MAX_TEXT);
    ModInfo* ordered = (ModInfo*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(ModInfo) * MAX_MODS);
    BOOL* used = (BOOL*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(BOOL) * MAX_MODS);
    if (!ids || !ordered || !used) {
        if (ids) HeapFree(GetProcessHeap(), 0, ids);
        if (ordered) HeapFree(GetProcessHeap(), 0, ordered);
        if (used) HeapFree(GetProcessHeap(), 0, used);
        HeapFree(GetProcessHeap(), 0, data);
        if (g_status) SetWindowTextW(g_status, L"Could not load profile: out of memory.");
        return FALSE;
    }
    int idCount = 0;
    char* p = data;
    if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF) p += 3;
    while (*p && idCount < MAX_MODS) {
        while (*p == '\r' || *p == '\n' || *p == ' ' || *p == '\t') ++p;
        char* start = p;
        while (*p && *p != '\r' && *p != '\n') ++p;
        char* end = p;
        while (end > start && (end[-1] == ' ' || end[-1] == '\t')) --end;
        if (end > start) {
            Utf8ToWide2(start, (int)(end - start), ids[idCount], MAX_TEXT);
            if (ids[idCount][0]) ++idCount;
        }
    }
    HeapFree(GetProcessHeap(), 0, data);

    int out = 0;
    for (int i = 0; i < idCount; ++i) {
        for (int m = 0; m < g_modCount; ++m) {
            if (!used[m] && _wcsicmp(g_mods[m].id, ids[i]) == 0) {
                ordered[out++] = g_mods[m];
                used[m] = TRUE;
                break;
            }
        }
    }
    for (int m = 0; m < g_modCount; ++m) {
        if (!used[m]) ordered[out++] = g_mods[m];
    }
    for (int i = 0; i < g_modCount; ++i) g_mods[i] = ordered[i];
    HeapFree(GetProcessHeap(), 0, ids);
    HeapFree(GetProcessHeap(), 0, ordered);
    HeapFree(GetProcessHeap(), 0, used);
    BOOL repaired = RepairDependencyOrderInPlace();
    if (rebuildList && g_list) RebuildListPreservingChecks(0);
    if (g_status) SetWindowTextW(g_status, repaired
        ? L"Profile loaded. Order was adjusted to satisfy dependencies."
        : L"Profile order loaded.");
    return repaired;
}

static BOOL LoadOrderFromPath(const WCHAR* path) {
    return LoadOrderFromPathCore(path, TRUE);
}

static void LoadNamedProfile(void) {
    WCHAR profile[MAX_PROFILE], path[MAX_PATH * 2], enabledPath[MAX_PATH * 2];
    GetCurrentProfileName(profile, _countof(profile));
    if (IsCurrentSettingsProfile(profile)) {
        RefreshList();
        SetWindowTextW(g_status, L"Reloaded current game settings from settings.save.");
        return;
    }
    GetProfilePath(profile, path, _countof(path));
    BOOL repaired = LoadOrderFromPath(path);
    GetProfileEnabledPath(profile, enabledPath, _countof(enabledPath));
    if (LoadEnabledFromPathToList(enabledPath)) {
        SetWindowTextW(g_status, repaired
            ? L"Named profile loaded with enabled mods. Order was adjusted to satisfy dependencies."
            : L"Profile loaded with enabled selections.");
    }
}

static BOOL StartOriginalCommand(void) {
    if (!g_steamExe[0]) return FALSE;

    AppendLogf(L"Start original command: exe=%ls args=%ls", g_steamExe, g_steamArgs);
    SHELLEXECUTEINFOW info = {0};
    info.cbSize = sizeof(info);
    info.lpFile = g_steamExe;
    info.lpParameters = g_steamArgs[0] ? g_steamArgs : NULL;
    info.lpDirectory = g_gameDir[0] ? g_gameDir : NULL;
    info.nShow = SW_SHOWNORMAL;
    if (ShellExecuteExW(&info)) return TRUE;

    AppendLogf(L"ShellExecuteEx original command failed: %lu", GetLastError());
    return FALSE;
}

static BOOL StartGame(void) {
    if (StartOriginalCommand()) return TRUE;

    WCHAR exe[MAX_PATH * 2];
    JoinPath(exe, _countof(exe), g_gameDir, L"SlayTheSpire2.exe");
    if (!FileExistsW2(exe)) {
        MessageBoxW(NULL, L"SlayTheSpire2.exe was not found. Please check the game directory.", L"ModTheSpire2", MB_ICONERROR);
        return FALSE;
    }
    AppendLogf(L"Start fallback game exe by ShellExecute: %ls", exe);
    HINSTANCE result = ShellExecuteW(NULL, L"open", exe, NULL, g_gameDir, SW_SHOWNORMAL);
    if ((INT_PTR)result <= 32) {
        AppendLogf(L"ShellExecute fallback failed: %ld", (LONG_PTR)result);
        return FALSE;
    }
    return TRUE;
}

static void Launch(BOOL modded) {
    GetWindowTextW(g_gamePath, g_gameDir, _countof(g_gameDir));
    GetWindowTextW(g_settingsPath, g_settingsFile, _countof(g_settingsFile));
    AppendLogf(L"Launch requested modded=%d gameDir=%ls settings=%ls", modded, g_gameDir, g_settingsFile);
    if (modded) {
        BOOL any = FALSE;
        for (int i = 0; i < g_modCount; ++i) if (ListView_GetCheckState(g_list, i)) any = TRUE;
        AppendLogf(L"Launch selected any=%d", any);
        if (!any && MessageBoxW(NULL, L"No mods are selected. Launch vanilla instead?", L"ModTheSpire2", MB_YESNO | MB_ICONQUESTION) != IDYES) {
            AppendLog(L"Launch canceled: no mods selected");
            return;
        }
        if (!any) modded = FALSE;
        if (modded) {
            for (int i = 0; i < g_modCount; ++i) {
                WCHAR depName[MAX_TEXT];
                if (ListView_GetCheckState(g_list, i) && HasMissingDependency(i, depName, _countof(depName))) {
                    WCHAR msg[MAX_TEXT * 2];
                    swprintf(msg, _countof(msg),
                             L"This mod requires a missing dependency: %ls",
                             depName);
                    MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
                    return;
                }
                WCHAR required[MAX_TEXT], actual[MAX_TEXT];
                if (ListView_GetCheckState(g_list, i) && HasTooLowDependencyVersion(i, depName, _countof(depName), required, _countof(required), actual, _countof(actual))) {
                    WCHAR msg[MAX_TEXT * 3];
                    swprintf(msg, _countof(msg),
                             L"This mod requires a newer dependency:\n\n%ls requires %ls, found %ls",
                             depName, required, actual);
                    MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
                    return;
                }
                if (ListView_GetCheckState(g_list, i) && HasTooLowGameVersion(i, required, _countof(required), actual, _countof(actual))) {
                    WCHAR msg[MAX_TEXT * 3];
                    swprintf(msg, _countof(msg),
                             L"This mod requires a newer Slay the Spire 2 version:\n\nRequires %ls, found %ls",
                             required, actual);
                    MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
                    return;
                }
                if (ListView_GetCheckState(g_list, i) && HasUncheckedKnownDependency(i, depName, _countof(depName))) {
                    WCHAR msg[MAX_TEXT * 2];
                    swprintf(msg, _countof(msg),
                             L"Select the required dependency first: %ls",
                             depName);
                    MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
                    return;
                }
            }
        }
    }
    WCHAR orderMessage[MAX_TEXT * 2];
    if (modded && !ValidateSelectedDependencyOrder(orderMessage, _countof(orderMessage))) {
        MessageBoxW(NULL, orderMessage, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        return;
    }
    if (modded && !SaveCurrentEnabled()) {
        MessageBoxW(NULL, L"Could not save enabled-mods.txt.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    WriteSettings(modded);
    if (StartGame()) {
        PostQuitMessage(0);
    } else {
        MessageBoxW(NULL, L"Failed to start the game. Please check launcher.log.", L"ModTheSpire2", MB_ICONERROR);
    }
}

static void EnforceDependencyChecks(int changedIndex) {
    if (g_enforcingChecks) return;
    if (changedIndex < 0 || changedIndex >= g_modCount) return;
    g_enforcingChecks = TRUE;
    BOOL checked = ListView_GetCheckState(g_list, changedIndex);

    if (checked) {
        WCHAR depName[MAX_TEXT];
        WCHAR required[MAX_TEXT], actual[MAX_TEXT];
        if (HasMissingDependency(changedIndex, depName, _countof(depName))) {
            ListView_SetCheckState(g_list, changedIndex, FALSE);
            WCHAR msg[MAX_TEXT * 2];
            swprintf(msg, _countof(msg),
                     L"This mod requires a missing dependency: %ls",
                     depName);
            MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        } else if (HasTooLowDependencyVersion(changedIndex, depName, _countof(depName), required, _countof(required), actual, _countof(actual))) {
            ListView_SetCheckState(g_list, changedIndex, FALSE);
            WCHAR msg[MAX_TEXT * 3];
            swprintf(msg, _countof(msg),
                     L"This mod requires a newer dependency:\n\n%ls requires %ls, found %ls",
                     depName, required, actual);
            MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        } else if (HasTooLowGameVersion(changedIndex, required, _countof(required), actual, _countof(actual))) {
            ListView_SetCheckState(g_list, changedIndex, FALSE);
            WCHAR msg[MAX_TEXT * 3];
            swprintf(msg, _countof(msg),
                     L"This mod requires a newer Slay the Spire 2 version:\n\nRequires %ls, found %ls",
                     required, actual);
            MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        } else if (HasUncheckedKnownDependency(changedIndex, depName, _countof(depName))) {
            ListView_SetCheckState(g_list, changedIndex, FALSE);
            WCHAR msg[MAX_TEXT * 2];
            swprintf(msg, _countof(msg),
                     L"Select the required dependency first: %ls",
                     depName);
            MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        }
    } else {
        UncheckDependentsRecursive(changedIndex);
    }
    g_enforcingChecks = FALSE;
    RefreshStatusColumn();
}

static void AddColumns(void) {
    LVCOLUMNW col = {0};
    col.mask = LVCF_TEXT | LVCF_WIDTH;
    col.cx = 280; col.pszText = (LPWSTR)L"Name"; ListView_InsertColumn(g_list, 0, &col);
    col.cx = 170; col.pszText = L"ID"; ListView_InsertColumn(g_list, 1, &col);
    col.cx = 85; col.pszText = (LPWSTR)L"Source"; ListView_InsertColumn(g_list, 2, &col);
    col.cx = 75; col.pszText = (LPWSTR)L"Type"; ListView_InsertColumn(g_list, 3, &col);
    col.cx = 140; col.pszText = (LPWSTR)L"Group"; ListView_InsertColumn(g_list, 4, &col);
    col.cx = 50; col.pszText = (LPWSTR)L"Order"; ListView_InsertColumn(g_list, 5, &col);
    col.cx = 135; col.pszText = (LPWSTR)L"Requirements"; ListView_InsertColumn(g_list, 6, &col);
    col.cx = 140; col.pszText = (LPWSTR)L"Status"; ListView_InsertColumn(g_list, 7, &col);
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp) {
    switch (msg) {
    case WM_CREATE:
        g_title = CreateWindowW(L"STATIC", L"ModTheSpire2", WS_CHILD | WS_VISIBLE, 18, 14, 260, 30, hwnd, NULL, g_instance, NULL);
        g_subtitle = CreateWindowW(L"STATIC", L"Choose mods for this launch. Use Move Up/Down to customize load order; dependencies must stay before dependents.", WS_CHILD | WS_VISIBLE, 20, 44, 940, 24, hwnd, NULL, g_instance, NULL);
        CreateWindowW(L"STATIC", L"Game", WS_CHILD | WS_VISIBLE, 20, 82, 90, 22, hwnd, NULL, g_instance, NULL);
        g_gamePath = CreateWindowW(L"EDIT", g_gameDir, WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL | ES_READONLY, 118, 78, 860, 26, hwnd, NULL, g_instance, NULL);
        CreateWindowW(L"STATIC", L"settings.save", WS_CHILD | WS_VISIBLE, 20, 116, 100, 22, hwnd, NULL, g_instance, NULL);
        g_settingsPath = CreateWindowW(L"EDIT", g_settingsFile, WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL | ES_READONLY, 118, 112, 860, 26, hwnd, NULL, g_instance, NULL);
        g_list = CreateWindowW(WC_LISTVIEWW, L"", WS_CHILD | WS_VISIBLE | WS_BORDER | LVS_REPORT | LVS_SINGLESEL, 20, 152, 958, 410, hwnd, NULL, g_instance, NULL);
        ListView_SetExtendedListViewStyle(g_list, LVS_EX_CHECKBOXES | LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);
        ListView_SetBkColor(g_list, RGB(255, 253, 247));
        ListView_SetTextBkColor(g_list, RGB(255, 253, 247));
        ListView_SetTextColor(g_list, g_textColor);
        AddColumns();
        CreateWindowW(L"STATIC", L"Load order", WS_CHILD | WS_VISIBLE, 20, 570, 86, 20, hwnd, NULL, g_instance, NULL);
        g_btnUp = CreateWindowW(L"BUTTON", L"Move Up", WS_CHILD | WS_VISIBLE, 20, 594, 88, 30, hwnd, (HMENU)103, g_instance, NULL);
        g_btnDown = CreateWindowW(L"BUTTON", L"Move Down", WS_CHILD | WS_VISIBLE, 116, 594, 100, 30, hwnd, (HMENU)104, g_instance, NULL);
        CreateWindowW(L"STATIC", L"Row", WS_CHILD | WS_VISIBLE, 236, 599, 32, 22, hwnd, NULL, g_instance, NULL);
        g_orderEdit = CreateWindowW(L"EDIT", L"1", WS_CHILD | WS_VISIBLE | WS_BORDER | ES_NUMBER, 272, 594, 50, 28, hwnd, (HMENU)107, g_instance, NULL);
        g_btnApplyOrder = CreateWindowW(L"BUTTON", L"Set", WS_CHILD | WS_VISIBLE, 330, 594, 60, 30, hwnd, (HMENU)108, g_instance, NULL);
        CreateWindowW(L"STATIC", L"Profile", WS_CHILD | WS_VISIBLE, 420, 570, 70, 20, hwnd, NULL, g_instance, NULL);
        g_profileCombo = CreateWindowW(WC_COMBOBOXW, L"", WS_CHILD | WS_VISIBLE | CBS_DROPDOWN | WS_VSCROLL, 420, 594, 338, 120, hwnd, (HMENU)109, g_instance, NULL);
        g_btnSaveOrder = CreateWindowW(L"BUTTON", L"Save", WS_CHILD | WS_VISIBLE, 768, 594, 92, 30, hwnd, (HMENU)105, g_instance, NULL);
        g_btnRefresh = CreateWindowW(L"BUTTON", L"Refresh", WS_CHILD | WS_VISIBLE, 20, 640, 92, 34, hwnd, (HMENU)100, g_instance, NULL);
        g_btnVanilla = CreateWindowW(L"BUTTON", L"Vanilla", WS_CHILD | WS_VISIBLE, 124, 640, 120, 34, hwnd, (HMENU)101, g_instance, NULL);
        g_btnLaunch = CreateWindowW(L"BUTTON", L"Launch Selected", WS_CHILD | WS_VISIBLE, 256, 640, 174, 34, hwnd, (HMENU)102, g_instance, NULL);
        g_status = CreateWindowW(L"STATIC", L"", WS_CHILD | WS_VISIBLE, 448, 648, 530, 24, hwnd, NULL, g_instance, NULL);
        if (g_font) {
            SendMessageW(hwnd, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_subtitle, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_gamePath, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_settingsPath, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_list, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_status, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnUp, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnDown, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnSaveOrder, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_orderEdit, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnApplyOrder, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_profileCombo, WM_SETFONT, (WPARAM)g_font, TRUE);
        }
        if (g_titleFont) SendMessageW(g_title, WM_SETFONT, (WPARAM)g_titleFont, TRUE);
        RefreshProfiles();
        RefreshList();
        break;
    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLOREDIT:
        SetTextColor((HDC)wp, g_textColor);
        SetBkColor((HDC)wp, g_bgColor);
        return (LRESULT)g_bgBrush;
    case WM_COMMAND:
        AppendLogf(L"WM_COMMAND id=%u code=%u hwnd=%p", LOWORD(wp), HIWORD(wp), (void*)lp);
        if (LOWORD(wp) == 100 || (HWND)lp == g_btnRefresh) RefreshList();
        if (LOWORD(wp) == 101 || (HWND)lp == g_btnVanilla) Launch(FALSE);
        if (LOWORD(wp) == 102 || (HWND)lp == g_btnLaunch) Launch(TRUE);
        if (LOWORD(wp) == 103 || (HWND)lp == g_btnUp) MoveSelectedMod(-1);
        if (LOWORD(wp) == 104 || (HWND)lp == g_btnDown) MoveSelectedMod(1);
        if (LOWORD(wp) == 105 || (HWND)lp == g_btnSaveOrder) SaveCurrentOrder();
        if (LOWORD(wp) == 108 || (HWND)lp == g_btnApplyOrder) ApplySelectedOrderNumber();
        if (LOWORD(wp) == 109 && HIWORD(wp) == CBN_SELCHANGE && !g_refreshingProfiles) LoadNamedProfile();
        break;
    case WM_NOTIFY:
        if (((LPNMHDR)lp)->hwndFrom == g_list && ((LPNMHDR)lp)->code == LVN_ITEMCHANGED) {
            NMLISTVIEW* nmlv = (NMLISTVIEW*)lp;
            if ((nmlv->uChanged & LVIF_STATE) && (nmlv->uNewState & LVIS_SELECTED) && g_orderEdit && nmlv->iItem >= 0) {
                WCHAR orderText[32];
                swprintf(orderText, _countof(orderText), L"%d", nmlv->iItem + 1);
                SetWindowTextW(g_orderEdit, orderText);
            }
            if ((nmlv->uChanged & LVIF_STATE) &&
                ((nmlv->uOldState ^ nmlv->uNewState) & LVIS_STATEIMAGEMASK)) {
                EnforceDependencyChecks(nmlv->iItem);
            }
        }
        break;
    case WM_DESTROY:
        if (g_bgBrush) DeleteObject(g_bgBrush);
        if (g_font) DeleteObject(g_font);
        if (g_titleFont) DeleteObject(g_titleFont);
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProcW(hwnd, msg, wp, lp);
    }
    return 0;
}

static void ParseArgs(LPWSTR cmdLine) {
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(cmdLine, &argc);
    BOOL pass = FALSE;
    for (int i = 0; i < argc; ++i) {
        if (!pass && wcscmp(argv[i], L"--") == 0) { pass = TRUE; continue; }
        if (!pass && wcscmp(argv[i], L"--diagnose") == 0) { g_diagnose = TRUE; continue; }
        if (!pass && wcscmp(argv[i], L"--self-test-order") == 0) { g_selfTestOrder = TRUE; continue; }
        if (!pass && wcscmp(argv[i], L"--self-test-settings") == 0) { g_selfTestSettings = TRUE; continue; }
        if (!pass && wcscmp(argv[i], L"--wait-for-pid") == 0 && i + 1 < argc) { g_waitForPid = wcstoul(argv[++i], NULL, 10); continue; }
        if (pass) {
            if (!g_steamExe[0]) wcscpy(g_steamExe, argv[i]);
            else {
                WCHAR q[1024];
                QuoteArg(q, _countof(q), argv[i]);
                if (g_steamArgs[0]) wcscat(g_steamArgs, L" ");
                wcscat(g_steamArgs, q);
            }
        }
    }
    if (argv) LocalFree(argv);
}

static int RunDiagnostics(void) {
    g_modCount = 0;
    LoadSavedOrder();
    LoadGameVersion();
    WCHAR localRoot[MAX_PATH * 2], workshopRoot[MAX_PATH * 2];
    swprintf(localRoot, _countof(localRoot), L"%ls\\mods", g_gameDir);
    FindWorkshopDir(workshopRoot, _countof(workshopRoot));

    AppendLog(L"Diagnostic scan begin");
    AppendLogf(L"Diagnostic appDir=%ls", g_appDir);
    AppendLogf(L"Diagnostic steamExe=%ls", g_steamExe);
    AppendLogf(L"Diagnostic gameDir=%ls", g_gameDir);
    AppendLogf(L"Diagnostic settings=%ls", g_settingsFile);
    AppendLogf(L"Diagnostic localRoot=%ls", localRoot);
    AppendLogf(L"Diagnostic workshopRoot=%ls", workshopRoot);

    DiscoverRoot(localRoot, L"Local");
    DiscoverRoot(workshopRoot, L"Workshop");
    SortModsByDependencies();
    ApplyModGroups();

    AppendLogf(L"Diagnostic total mods=%d", g_modCount);
    for (int i = 0; i < g_modCount && i < 80; ++i) {
        WCHAR depText[MAX_TEXT] = L"";
        WCHAR statusText[MAX_TEXT] = L"";
        BuildDependencyText(i, depText, _countof(depText));
        BuildStatusText(i, statusText, _countof(statusText));
        AppendLogf(L"Diagnostic mod[%d]=%ls id=%ls source=%ls group=%ls depth=%d minGame=%ls deps=%ls status=%ls", i, g_mods[i].name, g_mods[i].id, g_mods[i].source, g_mods[i].group, g_mods[i].depth, g_mods[i].minGameVersion, depText, statusText);
    }
    AppendLog(L"Diagnostic scan end");

    WCHAR message[MAX_PATH * 6];
    swprintf(message, _countof(message),
             L"ModTheSpire2 diagnostic complete.\n\n"
             L"Mods found: %d\n\n"
             L"App dir:\n%ls\n\n"
             L"Steam command exe:\n%ls\n\n"
             L"Game dir:\n%ls\n\n"
             L"Local root:\n%ls\n\n"
             L"Workshop root:\n%ls\n\n"
             L"Logs are written beside the launcher, or to %%TEMP%%\\ModTheSpire2Launcher.log if that folder is not writable.",
             g_modCount, g_appDir, g_steamExe, g_gameDir, localRoot, workshopRoot);
    MessageBoxW(NULL, message, L"ModTheSpire2 Diagnostics", MB_OK | MB_ICONINFORMATION);
    return g_modCount > 0 ? 0 : 2;
}

static int RunOrderSelfTest(void) {
    g_modCount = 0;
    LoadSavedOrder();
    LoadGameVersion();
    WCHAR localRoot[MAX_PATH * 2], workshopRoot[MAX_PATH * 2];
    swprintf(localRoot, _countof(localRoot), L"%ls\\mods", g_gameDir);
    FindWorkshopDir(workshopRoot, _countof(workshopRoot));
    AppendLog(L"Order self-test begin");
    DiscoverRoot(localRoot, L"Local");
    DiscoverRoot(workshopRoot, L"Workshop");
    SortModsByDependencies();
    ApplyModGroups();
    AppendLogf(L"Order self-test mods=%d", g_modCount);
    if (g_modCount < 2) {
        AppendLog(L"Order self-test failed: not enough mods");
        return 2;
    }

    if (!RunHiddenListMoveTest()) {
        return 16;
    }

    WCHAR firstId[MAX_TEXT], lastId[MAX_TEXT];
    wcscpy(firstId, g_mods[0].id);
    wcscpy(lastId, g_mods[g_modCount - 1].id);
    if (TryMoveIndexForTest(0, -1)) {
        AppendLog(L"Order self-test failed: first item moved up");
        return 3;
    }
    if (_wcsicmp(firstId, g_mods[0].id) != 0) {
        AppendLog(L"Order self-test failed: first item changed after boundary move");
        return 4;
    }
    if (TryMoveIndexForTest(g_modCount - 1, 1)) {
        AppendLog(L"Order self-test failed: last item moved down");
        return 5;
    }
    if (_wcsicmp(lastId, g_mods[g_modCount - 1].id) != 0) {
        AppendLog(L"Order self-test failed: last item changed after boundary move");
        return 6;
    }

    int independent = -1;
    for (int i = 1; i < g_modCount - 1; ++i) {
        if (g_mods[i].depCount == 0) {
            independent = i;
            break;
        }
    }
    if (independent >= 0) {
        WCHAR movingId[MAX_TEXT];
        wcscpy(movingId, g_mods[independent].id);
        TryMoveIndexForTest(independent, -1);
        int moved = FindModIndexById(movingId);
        if (moved < 0 || moved >= independent) {
            AppendLog(L"Order self-test failed: independent move up did not move");
            return 7;
        }
        TryMoveIndexForTest(moved, 1);
        moved = FindModIndexById(movingId);
        if (moved != independent) {
            AppendLog(L"Order self-test failed: independent move down did not restore");
            return 8;
        }
    }

    independent = -1;
    for (int i = 0; i < g_modCount; ++i) {
        if (g_mods[i].depCount == 0) {
            independent = i;
            break;
        }
    }
    if (independent >= 0) {
        WCHAR movedId[MAX_TEXT];
        wcscpy(movedId, g_mods[independent].id);
        MoveModToIndex(independent, g_modCount - 1);
        int moved = FindModIndexById(movedId);
        if (moved != g_modCount - 1) {
            AppendLog(L"Order self-test failed: numeric move to last did not land at last");
            return 11;
        }
        MoveModToIndex(moved, 0);
        moved = FindModIndexById(movedId);
        if (moved != 0) {
            AppendLog(L"Order self-test failed: numeric move to first did not land at first");
            return 12;
        }
        RepairDependencyOrderInPlace();
        WCHAR repairMessage[MAX_TEXT * 2];
        if (!ValidateDependencyOrder(repairMessage, _countof(repairMessage))) {
            AppendLogf(L"Order self-test failed after repair: %ls", repairMessage);
            return 13;
        }
    }

    int base = FindModIndexById(L"BaseLib");
    int quick = FindModIndexById(L"QuickRestart");
    if (base >= 0 && quick >= 0) {
        int target = quick + 1 < g_modCount ? quick + 1 : g_modCount - 1;
        int selected = target;
        BOOL repaired = FALSE;
        if (!ApplyNumericOrderForIndex(base, target, &selected, &repaired)) {
            AppendLog(L"Order self-test failed: numeric dependency repair move failed");
            return 18;
        }
        base = FindModIndexById(L"BaseLib");
        quick = FindModIndexById(L"QuickRestart");
        if (!repaired || base < 0 || quick < 0 || base > quick) {
            AppendLog(L"Order self-test failed: numeric dependency repair did not keep BaseLib before QuickRestart");
            return 19;
        }
        AppendLog(L"Order self-test numeric dependency repair passed");
        if (base > quick) {
            AppendLog(L"Order self-test failed: BaseLib after QuickRestart");
            return 9;
        }
        if (quick - base > 1) {
            AppendLog(L"Order self-test info: independent mod may exist between BaseLib and QuickRestart; dependency order is still valid");
        }
    }

    base = FindModIndexById(L"BaseLib");
    int loadAfter = FindModIndexById(L"LauncherLoadAfterOnly");
    if (base >= 0 && loadAfter >= 0) {
        if (!MoveModToIndex(base, loadAfter)) {
            AppendLog(L"Order self-test failed: could not perturb load_after order");
            return 20;
        }
        RepairDependencyOrderInPlace();
        base = FindModIndexById(L"BaseLib");
        loadAfter = FindModIndexById(L"LauncherLoadAfterOnly");
        if (base < 0 || loadAfter < 0 || base > loadAfter) {
            AppendLog(L"Order self-test failed: load_after repair did not keep BaseLib before LauncherLoadAfterOnly");
            return 21;
        }
        AppendLog(L"Order self-test load_after repair passed");
    } else {
        AppendLog(L"Order self-test load_after repair skipped: fixture not present");
    }

    int loadBefore = FindModIndexById(L"LauncherLoadBeforeOnly");
    loadAfter = FindModIndexById(L"LauncherLoadAfterOnly");
    if (loadBefore >= 0 && loadAfter >= 0) {
        if (!MoveModToIndex(loadBefore, loadAfter)) {
            AppendLog(L"Order self-test failed: could not perturb load_before order");
            return 22;
        }
        RepairDependencyOrderInPlace();
        loadBefore = FindModIndexById(L"LauncherLoadBeforeOnly");
        loadAfter = FindModIndexById(L"LauncherLoadAfterOnly");
        if (loadBefore < 0 || loadAfter < 0 || loadBefore > loadAfter) {
            AppendLog(L"Order self-test failed: load_before repair did not keep LauncherLoadBeforeOnly before LauncherLoadAfterOnly");
            return 23;
        }
        AppendLog(L"Order self-test load_before repair passed");
    } else {
        AppendLog(L"Order self-test load_before repair skipped: fixture not present");
    }

    WCHAR profilePath[MAX_PATH * 2];
    GetProfilePath(L"self-test", profilePath, _countof(profilePath));
    if (!SaveOrderToPath(profilePath)) {
        AppendLog(L"Order self-test failed: could not save self-test profile");
        return 14;
    }
    if (!FileExistsW2(profilePath)) {
        AppendLog(L"Order self-test failed: self-test profile missing after save");
        return 15;
    }
    DeleteFileW(profilePath);

    if (!RunProfileRepairSelfTest()) {
        return 17;
    }

    if (!RunDataFileBackupSelfTest()) {
        return 18;
    }

    WCHAR message[MAX_TEXT * 2];
    if (!ValidateDependencyOrder(message, _countof(message))) {
        AppendLogf(L"Order self-test failed: %ls", message);
        return 10;
    }
    AppendLog(L"Order self-test passed");
    return 0;
}

static int RunSettingsSelfTest(void) {
    WCHAR dataDir[MAX_PATH * 2], root[MAX_PATH * 2], oldDir[MAX_PATH * 2], newDir[MAX_PATH * 2];
    WCHAR oldFile[MAX_PATH * 2], newFile[MAX_PATH * 2], selected[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(root, _countof(root), dataDir, L"settings-self-test");
    CreateDirectoryW(root, NULL);
    JoinPath(oldDir, _countof(oldDir), root, L"steam\\11111111111111111");
    JoinPath(newDir, _countof(newDir), root, L"steam\\22222222222222222");
    CreateDirectoryW(root, NULL);
    WCHAR steamDir[MAX_PATH * 2];
    JoinPath(steamDir, _countof(steamDir), root, L"steam");
    CreateDirectoryW(steamDir, NULL);
    CreateDirectoryW(oldDir, NULL);
    CreateDirectoryW(newDir, NULL);
    JoinPath(oldFile, _countof(oldFile), oldDir, L"settings.save");
    JoinPath(newFile, _countof(newFile), newDir, L"settings.save");
    const char* oldJson = "{\"mods_enabled\":false}";
    const char* newJson = "{\"mods_enabled\":true}";
    WriteFileBytes(oldFile, oldJson, (DWORD)strlen(oldJson));
    Sleep(1100);
    WriteFileBytes(newFile, newJson, (DWORD)strlen(newJson));

    if (!FindNewestSettingsFileUnder(root, selected, _countof(selected))) {
        AppendLog(L"Settings self-test failed: no settings.save selected");
        return 21;
    }
    AppendLogf(L"Settings self-test selected=%ls", selected);
    if (_wcsicmp(selected, newFile) != 0) {
        AppendLogf(L"Settings self-test failed: expected newest file %ls", newFile);
        return 22;
    }
    const char* overlapJson =
        "{"
        "\"mods_enabled\":true,"
        "\"mod_list\":["
        "{\"id\":\"Hina\",\"is_enabled\":false},"
        "{\"id\":\"TenshiHinanawi\",\"is_enabled\":true},"
        "{\"id\":\"HinanawiHinaSkin\",\"is_enabled\":true}"
        "]"
        "}";
    if (JsonBoolAfterId(overlapJson, L"Hina")) {
        AppendLog(L"Settings self-test failed: exact disabled Hina id was treated as enabled");
        return 23;
    }
    if (!JsonBoolAfterId(overlapJson, L"TenshiHinanawi")) {
        AppendLog(L"Settings self-test failed: exact enabled TenshiHinanawi id was not detected");
        return 24;
    }
    if (JsonBoolAfterId(overlapJson, L"Tenshi")) {
        AppendLog(L"Settings self-test failed: partial id Tenshi matched TenshiHinanawi");
        return 25;
    }
    if (!JsonBoolAfterId(overlapJson, L"HinanawiHinaSkin")) {
        AppendLog(L"Settings self-test failed: exact enabled HinanawiHinaSkin id was not detected");
        return 26;
    }

    WCHAR oldSettingsFile[MAX_PATH * 2];
    wcsncpy(oldSettingsFile, g_settingsFile, _countof(oldSettingsFile) - 1);
    oldSettingsFile[_countof(oldSettingsFile) - 1] = 0;
    WCHAR vanillaFile[MAX_PATH * 2];
    JoinPath(vanillaFile, _countof(vanillaFile), root, L"vanilla-preserve-settings.save");
    const char* vanillaJson =
        "{"
        "\"mods_enabled\": true,"
        "\"mod_list\":["
        "{\"id\":\"BaseLib\",\"is_enabled\":true,\"source\":\"steam_workshop\"},"
        "{\"id\":\"QuickRestart\",\"is_enabled\":false,\"source\":\"steam_workshop\"}"
        "],"
        "\"tail\":\"keep\""
        "}";
    WriteFileBytes(vanillaFile, vanillaJson, (DWORD)strlen(vanillaJson));
    DWORD beforeLen = 0;
    char* beforeList = ReadModListSpanCopy(vanillaFile, &beforeLen);
    wcsncpy(g_settingsFile, vanillaFile, _countof(g_settingsFile) - 1);
    g_settingsFile[_countof(g_settingsFile) - 1] = 0;
    WriteSettings(FALSE);
    wcsncpy(g_settingsFile, oldSettingsFile, _countof(g_settingsFile) - 1);
    g_settingsFile[_countof(g_settingsFile) - 1] = 0;
    DWORD afterLen = 0, afterSize = 0;
    char* afterList = ReadModListSpanCopy(vanillaFile, &afterLen);
    char* afterJson = ReadFileBytes(vanillaFile, &afterSize);
    BOOL vanillaOk = beforeList && afterList && beforeLen == afterLen && memcmp(beforeList, afterList, beforeLen) == 0 &&
                     afterJson && strstr(afterJson, "\"mods_enabled\": false") != NULL &&
                     strstr(afterJson, "\"mods_enabled\": true") == NULL;
    if (beforeList) HeapFree(GetProcessHeap(), 0, beforeList);
    if (afterList) HeapFree(GetProcessHeap(), 0, afterList);
    if (afterJson) HeapFree(GetProcessHeap(), 0, afterJson);
    if (!vanillaOk) {
        AppendLog(L"Settings self-test failed: Vanilla write did not preserve mod_list exactly while disabling mods");
        return 27;
    }
    DeleteFileW(vanillaFile);
    AppendLog(L"Settings self-test passed");
    return 0;
}

static void WaitForPreviousGame(void) {
    if (!g_waitForPid) return;
    HANDLE h = OpenProcess(SYNCHRONIZE, FALSE, g_waitForPid);
    if (h) {
        WaitForSingleObject(h, 30000);
        CloseHandle(h);
        Sleep(800);
    }
}

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE prev, LPWSTR cmdLine, int show) {
    (void)prev;
    (void)cmdLine;
    g_instance = instance;
    GetModuleFileNameW(NULL, g_appDir, _countof(g_appDir));
    DirName(g_appDir);
    LPWSTR fullCommandLine = GetCommandLineW();
    if (fullCommandLine && wcsstr(fullCommandLine, L"--diagnose")) g_diagnose = TRUE;
    AppendLog(L"Boot");
    ParseArgs(fullCommandLine);
    AppendLogf(L"Launcher start. appDir=%ls waitForPid=%lu steamExe=%ls", g_appDir, g_waitForPid, g_steamExe);
    FindGameDir();
    LoadGameVersion();
    FindSettingsFile();
    WaitForPreviousGame();
    DetectLanguage();
    AppendLogf(L"Detected gameDir=%ls", g_gameDir);
    AppendLogf(L"Detected settings=%ls", g_settingsFile);

    if (g_diagnose) {
        return RunDiagnostics();
    }
    if (g_selfTestOrder) {
        return RunOrderSelfTest();
    }
    if (g_selfTestSettings) {
        return RunSettingsSelfTest();
    }

    INITCOMMONCONTROLSEX icc = { sizeof(icc), ICC_LISTVIEW_CLASSES };
    InitCommonControlsEx(&icc);
    WNDCLASSW wc = {0};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = instance;
    wc.lpszClassName = L"ModTheSpire2LauncherWindow";
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    g_bgBrush = CreateSolidBrush(g_bgColor);
    wc.hbrBackground = g_bgBrush ? g_bgBrush : (HBRUSH)(COLOR_WINDOW + 1);
    RegisterClassW(&wc);
    g_font = CreateFontW(-16, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
    g_titleFont = CreateFontW(-24, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
    HWND hwnd = CreateWindowW(wc.lpszClassName, L"ModTheSpire2", WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
                              CW_USEDEFAULT, CW_USEDEFAULT, 1015, 730, NULL, NULL, instance, NULL);
    ShowWindow(hwnd, show);
    UpdateWindow(hwnd);
    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return 0;
}
