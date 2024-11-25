using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

/// OrdinateAttributeOverrule.cs
/// 
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under terms of the MIT license.

namespace AcMgdLib.DatabaseServices.Examples
{
   /// <summary>
   /// An example demonstrating the use of the 
   /// BlockAttributeOverrule class.
   /// 
   /// This example will autonomously update specific
   /// attributes of specific blocks to display the X 
   /// and Y ordinates of the owning BlockReference's 
   /// insertion point. Additionally, the example will
   /// change the attribute color to red if the value
   /// it displays is negative.
   /// 
   /// The example targets a block named "TESTBLOCK", 
   /// having 2 attributes with the tags "X" and "Y",
   /// which is included in the accompanying sample
   /// DWG file.
   /// 
   /// The overrule functionality is enabled by issuing
   /// the ORDINATEATTRIB command.
   /// 
   /// Once the overrule is enabled, and there are one or more
   /// insertions of the TESTBLOCK in the current document, try
   /// using any of the following and note that the attributes
   /// will always display the X and Y ordinates of the owning
   /// block's insertion point:
   /// 
   ///     MOVE command
   ///     STRETCH command
   ///     GRIP_MOVE/STRETCH commands
   ///     COPY command
   ///     CUT/COPYCLIP and PASTECLIP commands
   ///     PROPERTIES Palette (any property change).
   ///     
   /// Revisions:
   /// 
   /// This code is based on code that was written in 2013, and
   /// that had some shortcomings related to performance overhead, 
   /// which have been addressed in this update.
   ///     
   /// </summary>

   internal class OrdinateAttributeOverrule : BlockAttributeOverrule
   {
      /// <summary>
      /// This class is a singleton:
      /// </summary>
      
      static OrdinateAttributeOverrule instance = null;

      /// <summary>
      /// Constructor:
      /// 
      /// The block name parameter is a wildcard that can be
      /// used to specify multiple block names using standard
      /// AutoCAD-style wildcard matching. The value supplied
      /// to the base constructor below will act on any block
      /// whose name contains the string 'TESTBLOCK'.
      /// </summary>

      public OrdinateAttributeOverrule()
         : base(true, "*TESTBLOCK*", "X", "Y")
      {
         /// Setting this property to true will cause the
         /// overrule to act on newly-inserted blocks, but
         /// fail to update them if the ATTDIA sysvar is 0.
         
         InsertEnabled = false; 
      }

      /// <summary>
      /// This example uses the second overload of Update()
      /// that passes all qualifying AttributeReferences in
      /// a dictionary keyed to their tags. Note that this
      /// code will fail if a block reference contains more
      /// than one attribute with the same tag. 
      /// 
      /// Sets the attribute's textstring to the corresponding
      /// ordinate of the block insertion point, and sets the 
      /// attribute's color to red if the displayed value is 
      /// negative:
      /// </summary>

      protected override void Update(Transaction tr, 
         BlockReference owner,
         Dictionary<string, AttributeReference> attributes)
      {
         Point3d pos = owner.Position;
         Point3d position = owner.Position;

         /// Compose the new TextStrings for each of
         /// the two attributes:
         string xtext = $"X = {position.X:0.000}";
         string ytext = $"Y = {position.Y:0.000}";

         /// Let's make the color of attributes that
         /// display negative values red:
         int xcolor = position.X < 0.0 ? 1 : 255;
         int ycolor = position.Y < 0.0 ? 1 : 255;

         /// Get the 'X' attribute and update it if needed:
         AttributeReference xAttrib = attributes["X"];
         if(xAttrib.TextString != xtext)
            xAttrib.TextString = xtext;
         if(xAttrib.ColorIndex != xcolor)
            xAttrib.ColorIndex = xcolor;
         
         /// Get the 'Y' attribute and update it if needed:
         AttributeReference yAttrib = attributes["Y"];
         if(yAttrib.TextString != ytext)
            xAttrib.TextString = ytext;
         if(yAttrib.ColorIndex != ycolor)
            yAttrib.ColorIndex = ycolor;
      }

      /// <summary>
      /// A command to enable/disable the overrule:
      /// </summary>
      
      [CommandMethod("ORDINATEATTRIB")]
      public static void StartStop()
      {
         if(instance == null)
            instance = new OrdinateAttributeOverrule();
         else
         {
            instance.Dispose();
            instance = null;
         }
         var what = instance != null ? "enabled" : "disabled";
         AppUtils.Write($"\nOrdinate Attribute Overrule {what}");
      }
   }


}
