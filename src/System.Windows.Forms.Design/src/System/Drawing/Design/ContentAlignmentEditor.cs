﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Windows.Forms.Design;

namespace System.Drawing.Design
{
    /// <summary>
    /// Provides a <see cref='UITypeEditor'/> for visually editing content alignment.
    /// </summary>
    public partial class ContentAlignmentEditor : UITypeEditor
    {
        /// <summary>
        /// Edits the given object value using the editor style provided by GetEditStyle.
        /// </summary>
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (provider is null)
            {
                return value;
            }

            var edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
            if (edSvc is null)
            {
                return value;
            }

            // To avoid scaling and resulting perf issues, we will be creating the UI everytime it is requested.
            using (ContentUI contentUI = new())
            {
                contentUI.Start(edSvc, value);
                edSvc.DropDownControl(contentUI);
                value = contentUI.Value;
                contentUI.End();
            }

            return value;
        }

        /// <summary>
        /// Gets the editing style of the Edit method.
        /// </summary>
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            => UITypeEditorEditStyle.DropDown;
    }
}

