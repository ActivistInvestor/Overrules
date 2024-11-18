using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;

/// HeadsUpAttributeOverrule.cs
/// 
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under terms of the MIT license.

namespace AcMgdLib.DatabaseServices.Examples
{
   /// <summary>
   /// An example that demonstrates the use of the 
   /// BlockAttributeOverrule base type.
   /// 
   /// This example class will autonomously update a
   /// specific attribute of a specific block to always 
   /// have a 'heads-up' orientation maintaining a text
   /// rotation of 0, relative to the WCS X-axis. 
   /// 
   /// The example targets a block named "BUBBLE", having 
   /// an attribute with the tag "ID", which can be found 
   /// in the accompanying test drawing file.
   /// 
   /// The overrule functionality is enabled by issuing
   /// the HEADSUPATTRIB command.
   /// 
   /// Once the overrule is enabled, and there are one or more
   /// insertions of the BUBBLE block in the current document, 
   /// try using any of the following operations to edit the
   /// BUBBLE block(s) and note that the attribute will always 
   /// maintain a rotation angle of 0, regardless of the owner 
   /// block reference's rotation/orientation.
   /// 
   ///    GRIP_ROTATE commands
   ///    ROTATE command
   ///    PROPERTIES Palette (block rotation)
   ///     
   /// </summary>

   public class HeadsUpAttributeOverrule : BlockAttributeOverrule
   {
      static HeadsUpAttributeOverrule instance = null;

      /// <summary>
      /// The block name parameter is a wildcard that can be
      /// used to specify multiple block names using standard
      /// AutoCAD-style wildcard matching. The value supplied
      /// to the base constructor below will act on any block
      /// whose name contains the string 'BUBBLE'.
      /// </summary>

      public HeadsUpAttributeOverrule()
         : base(true, "*BUBBLE*", "ID")
      {
         this.InsertEnabled = false;  // Do not act during -INSERT command.
      }

      /// <summary>
      /// The reusability baked into the base types allows the 
      /// operation to be implemented with 2 lines of code:
      /// </summary>

      protected override void Update(AttributeReference att, BlockReference owner)
      {
         if(att.Rotation != 0.0)
            att.Rotation = 0.0;
      }

      /// <summary>
      /// A command to enable/disable the overrule:
      /// </summary>
      
      [CommandMethod("HEADSUPATTRIB")]
      public static void StartStop()
      {
         if(instance == null)
            instance = new HeadsUpAttributeOverrule();
         else
         {
            instance.Dispose();
            instance = null;
         }
         var what = instance != null ? "enabled" : "disabled";
         AppUtils.Write($"\nHeads-up Attribute Overrule {what}");
      }
   }


}
