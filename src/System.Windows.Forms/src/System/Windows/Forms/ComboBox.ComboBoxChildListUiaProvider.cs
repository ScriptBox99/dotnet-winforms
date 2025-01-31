﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Accessibility;
using static System.Windows.Forms.ComboBox.ObjectCollection;
using static Interop;

namespace System.Windows.Forms;

public partial class ComboBox
{
    /// <summary>
    ///  Represents the ComboBox's child (inner) list native window control accessible object with UI Automation provider functionality.
    /// </summary>
    internal sealed class ComboBoxChildListUiaProvider : ChildAccessibleObject
    {
        private const string COMBO_BOX_LIST_AUTOMATION_ID = "1000";

        private readonly ComboBox _owningComboBox;
        private readonly IntPtr _childListControlhandle;

        public ComboBoxChildListUiaProvider(ComboBox owningComboBox, IntPtr childListControlhandle) : base(owningComboBox, childListControlhandle)
        {
            _owningComboBox = owningComboBox;
            _childListControlhandle = childListControlhandle;
        }

        private protected override string AutomationId => COMBO_BOX_LIST_AUTOMATION_ID;

        internal override Rectangle BoundingRectangle
        {
            get
            {
                PInvoke.GetWindowRect(_owningComboBox.GetListNativeWindow(), out var rect);
                return rect;
            }
        }

        internal override unsafe UiaCore.IRawElementProviderFragment? ElementProviderFromPoint(double x, double y)
        {
            using var accessible = SystemIAccessible.TryGetIAccessible(out HRESULT result);
            if (result.Succeeded)
            {
                result = accessible.Value->accHitTest((int)x, (int)y, out VARIANT child);
                if (result.Succeeded && child.vt == VARENUM.VT_I4)
                {
                    return GetChildFragment((int)child - 1);
                }
                else
                {
                    child.Dispose();
                    return null;
                }
            }

            return base.ElementProviderFromPoint(x, y);
        }

        internal override UiaCore.IRawElementProviderFragment? FragmentNavigate(NavigateDirection direction)
        {
            if (!_owningComboBox.IsHandleCreated ||
                // Created is set to false in WM_DESTROY, but the window Handle is released on NCDESTROY, which comes after DESTROY.
                // But between these calls, AccessibleObject can be recreated and might cause memory leaks.
                !_owningComboBox.Created)
            {
                return null;
            }

            switch (direction)
            {
                case NavigateDirection.NavigateDirection_Parent:
                    return _owningComboBox.AccessibilityObject;
                case NavigateDirection.NavigateDirection_FirstChild:
                    return GetChildFragment(0);
                case NavigateDirection.NavigateDirection_LastChild:
                    var childFragmentCount = GetChildFragmentCount();
                    if (childFragmentCount > 0)
                    {
                        return GetChildFragment(childFragmentCount - 1);
                    }

                    return null;
                case NavigateDirection.NavigateDirection_NextSibling:
                    return _owningComboBox.DropDownStyle == ComboBoxStyle.DropDownList
                        ? _owningComboBox.ChildTextAccessibleObject
                        : _owningComboBox.ChildEditAccessibleObject;
                case NavigateDirection.NavigateDirection_PreviousSibling:
                    // A workaround for an issue with an Inspect not responding. It also simulates native control behavior.
                    return _owningComboBox.DropDownStyle == ComboBoxStyle.Simple
                        ? _owningComboBox.ChildListAccessibleObject
                        : null;
                default:
                    return base.FragmentNavigate(direction);
            }
        }

        internal override UiaCore.IRawElementProviderFragmentRoot FragmentRoot => _owningComboBox.AccessibilityObject;

        public AccessibleObject? GetChildFragment(int index)
        {
            if (index < 0 || index >= _owningComboBox.Items.Count)
            {
                return null;
            }

            if (_owningComboBox.AccessibilityObject is not ComboBoxAccessibleObject comboBoxAccessibleObject)
            {
                return null;
            }

            Entry item = _owningComboBox.Entries[index];

            return item is null ? null : comboBoxAccessibleObject.ItemAccessibleObjects.GetComboBoxItemAccessibleObject(item);
        }

        public int GetChildFragmentCount()
        {
            return _owningComboBox.Items.Count;
        }

        internal override object? GetPropertyValue(UIA_PROPERTY_ID propertyID) =>
            propertyID switch
            {
                UIA_PROPERTY_ID.UIA_ControlTypePropertyId => UIA_CONTROLTYPE_ID.UIA_ListControlTypeId,
                UIA_PROPERTY_ID.UIA_HasKeyboardFocusPropertyId =>
                    // Narrator should keep the keyboard focus on th ComboBox itself but not on the DropDown.
                    false,
                UIA_PROPERTY_ID.UIA_IsEnabledPropertyId => _owningComboBox.Enabled,
                UIA_PROPERTY_ID.UIA_IsKeyboardFocusablePropertyId => (State & AccessibleStates.Focusable) == AccessibleStates.Focusable,
                UIA_PROPERTY_ID.UIA_IsOffscreenPropertyId => false,
                UIA_PROPERTY_ID.UIA_IsSelectionPatternAvailablePropertyId => true,
                UIA_PROPERTY_ID.UIA_NativeWindowHandlePropertyId => _childListControlhandle,
                _ => base.GetPropertyValue(propertyID)
            };

        internal override UiaCore.IRawElementProviderFragment? GetFocus() => GetFocused();

        public override AccessibleObject? GetFocused()
        {
            if (!_owningComboBox.IsHandleCreated)
            {
                return null;
            }

            int selectedIndex = _owningComboBox.SelectedIndex;
            return GetChildFragment(selectedIndex);
        }

        internal override UiaCore.IRawElementProviderSimple[] GetSelection()
        {
            if (!_owningComboBox.IsHandleCreated)
            {
                return Array.Empty<UiaCore.IRawElementProviderSimple>();
            }

            int selectedIndex = _owningComboBox.SelectedIndex;

            AccessibleObject? itemAccessibleObject = GetChildFragment(selectedIndex);

            if (itemAccessibleObject is not null)
            {
                return new UiaCore.IRawElementProviderSimple[]
                {
                itemAccessibleObject
                };
            }

            return Array.Empty<UiaCore.IRawElementProviderSimple>();
        }

        internal override bool CanSelectMultiple => false;

        internal override bool IsSelectionRequired => true;

        internal override bool IsPatternSupported(UIA_PATTERN_ID patternId)
        {
            switch (patternId)
            {
                case UIA_PATTERN_ID.UIA_LegacyIAccessiblePatternId:
                case UIA_PATTERN_ID.UIA_SelectionPatternId:
                    return true;
                default:
                    return base.IsPatternSupported(patternId);
            }
        }

        internal override UiaCore.IRawElementProviderSimple HostRawElementProvider
        {
            get
            {
                UiaCore.UiaHostProviderFromHwnd(new HandleRef(this, _childListControlhandle), out UiaCore.IRawElementProviderSimple provider);
                return provider;
            }
        }

        internal override int[] RuntimeId
            => new int[]
            {
                RuntimeIDFirstItem,
                PARAM.ToInt(_owningComboBox.InternalHandle),
                _owningComboBox.GetListNativeWindowRuntimeIdPart()
            };

        public override AccessibleStates State
        {
            get
            {
                AccessibleStates state = AccessibleStates.Focusable;
                if (_owningComboBox.Focused)
                {
                    state |= AccessibleStates.Focused;
                }

                return state;
            }
        }
    }
}
