#include <windows.h>
#include <setupapi.h>
#include <cfgmgr32.h>
#include <iostream>
#include <fstream>
#include <vector>
#include <string>
#include <locale>
#include <clocale>
#include <shlobj.h> // デスクトップパス取得用

#pragma comment(lib, "setupapi.lib")
#pragma comment(lib, "cfgmgr32.lib")
#pragma comment(lib, "shell32.lib")

// 指定したDevInstのFriendlyNameまたはDeviceDescを取得
std::wstring GetDeviceFriendlyName(HDEVINFO hDevInfo, SP_DEVINFO_DATA& devInfoData) {
    WCHAR buffer[256];
    DWORD requiredSize = 0;
    if (SetupDiGetDeviceRegistryPropertyW(
        hDevInfo,
        &devInfoData,
        SPDRP_FRIENDLYNAME,
        nullptr,
        (PBYTE)buffer,
        sizeof(buffer),
        &requiredSize
    )) {
        return buffer;
    }
    // FriendlyNameがない場合はDeviceDesc
    if (SetupDiGetDeviceRegistryPropertyW(
        hDevInfo,
        &devInfoData,
        SPDRP_DEVICEDESC,
        nullptr,
        (PBYTE)buffer,
        sizeof(buffer),
        &requiredSize
    )) {
        return buffer;
    }
    return L"(取得不可)";
}

// デバイスツリーを再帰的に表示（ファイル出力）
void PrintDeviceTree(HDEVINFO hDevInfo, DEVINST devInst, std::wofstream& ofs, int depth = 0) {
    SP_DEVINFO_DATA devInfoData = {};
    ULONG status = 0, problem = 0;
    devInfoData.cbSize = sizeof(SP_DEVINFO_DATA);
    auto a = CM_Get_DevNode_Status(&status, &problem, devInst, 0);
    if (a == CR_SUCCESS) {
        // Device Instance ID取得
        WCHAR deviceId[MAX_DEVICE_ID_LEN];
        if (CM_Get_Device_IDW(devInst, deviceId, MAX_DEVICE_ID_LEN, 0) != CR_SUCCESS) {
            wcscpy_s(deviceId, L"(取得失敗)");
        }

        // SP_DEVINFO_DATAを取得
        DWORD index = 0;
        bool found = false;
        while (SetupDiEnumDeviceInfo(hDevInfo, index, &devInfoData)) {
            ++index;
            WCHAR tmpId[MAX_DEVICE_ID_LEN];
            if (CM_Get_Device_IDW(devInfoData.DevInst, tmpId, MAX_DEVICE_ID_LEN, 0) == CR_SUCCESS) {
                if (wcscmp(tmpId, deviceId) == 0) {
                    found = true;
                    break;
                }
            }
        }
        // インデント
        for (int i = 0; i < depth; ++i) ofs << L"  ";
        ofs << L"[" << deviceId << L"] ";
        if (found) {
            ofs << GetDeviceFriendlyName(hDevInfo, devInfoData);
        }
        else {
            ofs << L"(FriendlyName取得不可)";
        }
        ofs << std::endl;
    }

    // 子デバイスを再帰的に表示
    DEVINST childDevInst;
    if (CM_Get_Child(&childDevInst, devInst, 0) == CR_SUCCESS) {
        PrintDeviceTree(hDevInfo, childDevInst, ofs, depth + 1);
        // 兄弟デバイスをたどる
        DEVINST siblingDevInst = childDevInst;
        while (CM_Get_Sibling(&siblingDevInst, siblingDevInst, 0) == CR_SUCCESS) {
            PrintDeviceTree(hDevInfo, siblingDevInst, ofs, depth + 1);
        }
    }
}

int main()
{
    std::setlocale(LC_ALL, "ja_JP.UTF-8");

    // デスクトップパス取得
    WCHAR desktopPath[MAX_PATH];
    if (SHGetSpecialFolderPathW(nullptr, desktopPath, CSIDL_DESKTOPDIRECTORY, FALSE) == FALSE) {
        std::wcerr << L"デスクトップパスの取得に失敗しました。" << std::endl;
        return 1;
    }
    std::wstring filePath = desktopPath;
    filePath += L"\\device_tree.txt";

    // ファイル出力ストリームを開く
    std::wofstream ofs(filePath);
    if (!ofs) {
        std::wcerr << L"ファイルの作成に失敗しました: " << filePath << std::endl;
        return 1;
    }
    ofs.imbue(std::locale("ja_JP.UTF-8"));

    // すべてのデバイスを取得
    HDEVINFO hDevInfo = SetupDiGetClassDevsA(
        nullptr, // すべてのクラス
        nullptr,
        nullptr,
        DIGCF_ALLCLASSES | DIGCF_PRESENT
    );
    if (hDevInfo == INVALID_HANDLE_VALUE) {
        ofs << L"SetupDiGetClassDevsA failed." << std::endl;
        return 1;
    }

    // ルートデバイスからツリー表示
    DEVINST rootDevInst;
    if (CM_Locate_DevNodeW(&rootDevInst, nullptr, 0) == CR_SUCCESS) {
        PrintDeviceTree(hDevInfo, rootDevInst, ofs, 0);
    }
    else {
        ofs << L"ルートデバイスの取得に失敗しました。" << std::endl;
    }

    SetupDiDestroyDeviceInfoList(hDevInfo);
    ofs.close();
    return 0;
}
