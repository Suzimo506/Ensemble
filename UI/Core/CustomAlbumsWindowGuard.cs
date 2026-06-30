using System;
using System.Reflection;
using MDEN.Managers;
using UnityEngine;

namespace MDEN.UI.Core
{
    internal static class CustomAlbumsWindowGuard
    {
        private const string LibraryWindowTypeName = "CustomAlbums.UI.LibraryWindow";
        private const string LibraryWindowAssemblyName = "CustomAlbums";
        private const string RootName = "CustomAlbumsLibraryWindow";
        private static Type _libraryWindowType;
        private static PropertyInfo _isOpenProperty;
        private static MethodInfo _closeMethod;

        public static bool CloseIfOpen(string context)
        {
            if (!IsOpen()) return false;

            ClientLogManager.Warning($"CustomAlbums library window is open during {context}; closing it before multiplayer flow continues.");
            if (!TryCloseByApi())
            {
                DestroyRootFallback();
            }
            else if (FindRootSlow() != null)
            {
                DestroyRootFallback();
            }

            return true;
        }

        public static bool IsOpen()
        {
            if (TryReadIsOpenByApi(out var isOpen))
            {
                return isOpen || FindRootFast() != null;
            }

            return FindRootFast() != null;
        }

        private static bool TryReadIsOpenByApi(out bool isOpen)
        {
            isOpen = false;
            try
            {
                var type = GetLibraryWindowType();
                if (type == null) return false;

                _isOpenProperty ??= type.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.Static);
                if (_isOpenProperty == null) return false;

                isOpen = _isOpenProperty.GetValue(null) is bool value && value;
                return true;
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Read CustomAlbums window state failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryCloseByApi()
        {
            try
            {
                var type = GetLibraryWindowType();
                if (type == null) return false;

                _closeMethod ??= type.GetMethod("Close", BindingFlags.Public | BindingFlags.Static);
                if (_closeMethod == null) return false;

                _closeMethod.Invoke(null, null);
                return true;
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Close CustomAlbums window failed: {ex.Message}");
                return false;
            }
        }

        private static Type GetLibraryWindowType()
        {
            if (_libraryWindowType != null) return _libraryWindowType;

            _libraryWindowType = Type.GetType($"{LibraryWindowTypeName}, {LibraryWindowAssemblyName}", false);
            if (_libraryWindowType != null) return _libraryWindowType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, LibraryWindowAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _libraryWindowType = assembly.GetType(LibraryWindowTypeName, false);
                if (_libraryWindowType != null) return _libraryWindowType;
            }

            return null;
        }

        private static void DestroyRootFallback()
        {
            var root = FindRootSlow();
            if (root == null) return;

            UnityEngine.Object.Destroy(root);
            NativeInputBlocker.ClearAll();
        }

        private static GameObject FindRootFast()
        {
            return GameObject.Find(RootName);
        }

        private static GameObject FindRootSlow()
        {
            var activeRoot = FindRootFast();
            if (activeRoot != null) return activeRoot;

            var objects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in objects)
            {
                if (obj != null && obj.name == RootName)
                {
                    return obj;
                }
            }

            return null;
        }
    }
}
