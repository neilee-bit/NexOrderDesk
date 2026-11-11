# NEX ORDER DESK 2.9 (native WPF, .NET 8)

Open **NexOrderDesk 2.0 version.sln** in Visual Studio 2022 (workload ".NET desktop development"), set configuration Debug, press F5.
Or double-click **build.bat** (needs the .NET 8 SDK) - it builds, starts the app, and writes build_errors.txt if anything fails.

Tips
- Extract the zip to a short path such as C:\NexOrderDesk (not a deep Downloads path) and right-click the zip > Properties > Unblock before extracting.
- "The debug executable ... does not exist" only means the build failed. Open View > Error List, build once (Ctrl+Shift+B) and read the first error.
- Settings (option lists, last order number) are stored in %LocalAppData%\NexOrderDesk\settings.json.

Features: order tabs (T-Shirt, Jacket, Cut and Print, Print and Press, Bottoms), editable option lists, image upload/drag-drop/paste, size tables with live totals, Submit/Edit.
Added in this build
- Save button replaced by **Print** (Ctrl+P): always A5 landscape; prints the front, plus the back when it has content.
- **PDF** button: downloads the order as an A5-landscape PDF (front + back if used).
- **Flip** button: switches the sheet to its back side - an Excel-like grid. Click a cell and type; Ctrl+V pastes ranges from Excel/Word/web
  (and pictures); Enter/arrows move between cells; drag column edges to resize; A-/A+ text size; +/- Row/Col; pictures can be dragged and resized.
- Size table: Adults (S/S), Adults (L/S), Kids (S/S) and, with "+ PS sizes", a 4th column PS (S/S) with P4XS-PXL. Headers and size labels are black.
- Output look (after Submit, in print and PDF): no dropdown arrows, +/- buttons or image outlines; last order number sits faintly at the top right.
- Fixed: picking a date in the calendar no longer opens the image-upload dialog underneath.

VERSION, INSTALLERS AND ONLINE RELEASES  ->  full guide: RELEASING.md
- Version, GitHub repository and minimum supported version: Directory.Build.props (the only place to change them).
- build_msi.bat         -> release\NexOrderDesk_<version>_Setup.msi       (light, 2-4 MB, needs .NET 8 Desktop Runtime x64)
- build_msi.bat full    -> release\NexOrderDesk_<version>_Setup_Full.msi  (self-contained, 60-70 MB)
- build_release.bat     -> both MSIs + update.json + SHA256SUMS.txt + RELEASE_NOTES.md in the "release" folder
- GitHub: push a tag like v2.7.1 -> .github/workflows/release.yml builds and uploads everything as a draft release.
- App icon: src\NexOrderDesk\Assets\app.ico

New in 2.9
- Readable text on dark theme colours (order number, totals, print method …): white on dark, dark on light.
- Material colour: type a colour name, the swatch is found automatically (no colour codes).
- Print and Press: only Collar Patch / Placket (tick + colour, shown when ticked) and Piping.
- Bottoms: 4XS-5XL + Kids and PS size lines. 18 more apparel icons. Ctrl+N new order.
- Bold selections, slim suggestion scroll bars, single picture fills its area, long names shrink to fit.

New in 2.8
- Fixed: the selected coordinator was erased when the sheet was drawn for Submit / Print / PDF - now always on the order.
- Print and PDF buttons only appear after Submit (hidden while creating / editing; Ctrl+P too).
- Adult size columns: category (Adults / Unisex / Gents / Lads) and sleeve (S/S / L/S) drop-downs in the column headers.
- Submitted sheet / print / PDF: size columns without quantities are left out; bottoms sizes sit next to the total.
- Customer field suggests customers while typing (29 included, new names remembered, list editable).
- Self-test: NexOrderDesk.exe --selftest result.txt (run by the GitHub test build on every push).

New in 2.7
- Online updates via GitHub Releases: "Check for updates" in the top bar, optional check at start-up (small
  "Update x.y.z available" button), shows installed / new version, release date and what's new.
- Download with progress, then size + SHA256 + MSI-file checks before anything runs. Install with Windows Installer
  (normal UAC prompt), app closes first and restarts after a successful update, same install folder, user data kept.
- One version number for app, MSI, tag and manifest (Directory.Build.props / the Git tag).
- Release builder (tools/build-release.ps1), GitHub Actions workflow, release-notes folder, RELEASING.md guide.

New in 2.6
- Placket Type & Color row: [label] [drop-down 3 Pcs Placket (default) / 2 Pcs Placket] [text box "T-shirts match"].
  Button & Button Color row: [label] [drop-down 2 (default) / 3] [text box "T-shirts match"]. No colour pickers.
  Both drop-down lists are add/editable. Click or tab into a text box that still says "T-shirts match" and it is
  selected, so the first key you type replaces it. Edit, clear or type anything; exactly what is in the box prints.
- Piping row: [tick box + label] [text]. Unticked shows "No piping"; ticked lets you type the piping details / colour.
- Crop tool for pictures (front / back image areas, back side, DTF and flag workspaces):
  select a picture and click its scissors button (bottom-left), double-click it, or use "Crop" in the workspace toolbar.
  Drag inside the box to move it, edges / corners to resize; shape presets Free, Original, 1:1, 4:3, 3:4, 16:9;
  zoom with - / + / slider / mouse wheel, Fit, 100%; drag outside the box to move the picture. Apply or Cancel.
  Cropping never changes the uploaded picture: open the crop tool again and press "Restore original" to get the whole
  picture back. The picture keeps its scale on the sheet; move, resize, rotate, Ctrl+C / Ctrl+V all keep working
  (Ctrl+C gives other apps the cropped picture; pasting inside the app keeps the original + crop so it stays editable).

New in 2.5
- BUILD FIXES: two compile errors fixed (a misplaced "Rotation" property in the workspace model and a missing namespace
  in the sheet banner) - these stopped the build, which is why Visual Studio said the .exe "does not exist".
  The app now needs NO extra NuGet packages at all (builds fully offline): SVG pictures are read by a small built-in
  SVG reader, WEBP / HEIC use the codecs built into Windows 10 / 11.
- "Placket Type & Color" and "Button & Button Color" are free text boxes. Empty = "T-shirts match" (grey hint while editing,
  printed on the sheet). As soon as you type, the hint disappears.
- FIXED sizes: clearing a quantity no longer shows a red box, and the number is really removed - the total updates
  (back to "Total: 0"). Same fix for the bottoms size strip and flag quantities.
- Front / back image areas: free space, no frame or card around them, pictures are never cropped. Every picture and text
  box (image areas, back side, DTF, flags) can now be ROTATED: drag the round handle above it (snaps to straight angles,
  Shift = 15° steps, double-click the handle = straight) or use "⟳ 90°" in the workspace toolbar.
- New JACKET sheet layout (used by "Jacket Order"): Jacket Type, Collar (Hooded is highlighted), Sleeve & Cuff, Closure,
  Pocket Type (tick one or more, "None" clears the others) and Additional Customisation tick boxes (only ticked ones print).
  Collar Color / Placket / Button / Piping rows are not on the jacket sheet. Kids sizes are hidden like PS sizes: switch
  them on with "+ Kids". All jacket lists are add/editable. Existing settings: Jacket Order is switched to this layout once.
- Your own order icons: click the icon on the sheet banner (while editing), or in Order types use "Upload my own icon…".
  Any picture works (PNG, JPG, SVG, ICO, WEBP). "Use built-in icon" goes back. Shown on the banner, the tabs and the
  New Order menu.
- Visual Studio: added a normal debug profile (Properties\launchSettings.json). If VS ever says the debug executable
  "does not exist", choose Build > Rebuild Solution first (or just run build.bat) - it means that configuration
  (Debug / Release) has not been built yet.

New in 2.3
- Front image cards: pictures are now free objects - drag to move, corner to resize (proportions kept), several per card.
  Click a card, then Ctrl+V pastes into it. Select a picture and Ctrl+C copies it (paste it again, into another card, a
  workspace, or another app). Double-click a card, or use its small upload button (top-right), to browse. Drag & drop works.
  The bin button clears the card. Workspaces (back side, DTF, flags) support Ctrl+C on pictures too.
- "Placket Type & Color:" - type on the left (3 Pcs Placket default, 2 Pcs Placket, add/edit), colour on the right.
- "Button & Button Color:" - button count on the left (2 default, 3, add/edit), colour on the right.
- Piping is optional: tick "Piping & Piping Color" to choose type + colour; unticked shows "No piping".
  Colours for placket, buttons and piping come from the shared, editable colour list.
- Back side paper: A5 (default) or A4 switch in the back-side title bar. A4 makes the back side an A4 portrait page with more
  room; objects are kept inside when switching back. An A4 back side always prints / saves on its own A4 page.
- Special notes box slightly smaller.

New in 2.2
- Back side: no more Excel grid. A clean order form: title, order reference strip (order no, name, customer, delivery) and a free
  workspace. Ctrl+V pastes pictures OR text (text becomes a movable text box), drag & drop picture files, "Images..." imports
  several at once. Text boxes: click once to type, drag the edge or the small tab to move, corner to change width,
  A-/A+/B/colour in the toolbar. Pictures: drag to move, corner to resize (proportions kept, never distorted). Del removes.
- All common picture formats everywhere: PNG, JPG/JPEG, WEBP, SVG (stays sharp), GIF, BMP, TIFF, ICO (+HEIC/AVIF with the
  Windows codec). The front image cards also take several files at once (fills front + back image).
- Special notes: no scroll bars - long notes shrink to fit the box. Notes area is larger.
- Bottoms / shorts: sizes now run left-to-right in one compact strip with the TOTAL beside it; "+ Other sizes" and "+ size" add
  optional sizes. Printing-method panel is smaller. Material colour picker is bigger and easier. New "Shorts Order" type.
- NEW order type FLAGS: flag artwork workspace, FLAG SIZES table (width x height, ft / in switch, single / double sided, qty,
  up to 10 sizes, total), flag materials (Satin, Silk, Polyester Silk, Chiffon Silk, Imitation Silk - add/edit) with colour
  in a bar at the bottom (up to 4 materials). No garment sizes, print methods or notes.
- NEW order type DTF: only Order No, Delivery Date, Order Name, Customer, Coordinator + one big workspace for artwork
  (paste / drop / import pictures and text, move and resize freely).
- Submit: the sheet locks and its PDF is saved automatically to Documents\NexOrderDesk Orders (Order_<no>_<name>.pdf).
  No print or PDF window opens. Re-submitting the same order replaces its file (no duplicates). A small note confirms it
  with an "Open folder" button.
- Print / PDF window with live A4 preview:
  A. Single sheet - full A4 (landscape)
  B. Two copies on one A4 - top + bottom halves, dashed cut line
  C. Two copies, double-sided - same layout on both sides (side 2 = back side, or the front again), long-edge duplex, aligned
  plus A5 landscape and Half A4 (sheet at the top). PDF uses exactly the same layouts. The choice is remembered and used
  for the automatic PDF on Submit. Ctrl+P opens it.
- Existing settings are kept; SHORTS, FLAGS and DTF are added to your order types once.
- (2.5: no NuGet packages needed any more.)

New in 2.1
- FIXED: Delivery date - the picked date now always shows on the sheet. Quick picks: Today, +3 days, +1 week, +2 weeks.
  The calendar no longer "eats" the next click on the sheet.
- FIXED: "+ Add / edit list..." no longer crashes the app. New, cleaner list editor: type + Enter to add, Rename, Delete (asks first),
  move up/down; colours list has a colour palette + #hex box. A safety net now catches any unexpected error and logs it to
  %LocalAppData%\NexOrderDesk\error_log.txt instead of closing the app.
- New order types: in  +  (New order) > "+ Add / edit order types & colours..." add your own order types, choose the sheet layout
  (Tops or Bottoms), icon, sheet title and THEME COLOUR (palette or #hex). Built-in types can be recoloured too.
- Per-order colour: the small palette button on the right of the sheet banner changes the theme colour of that one order
  (Reset, or "Save as default for this order type"). Hidden after Submit and in print/PDF.
- Material & colour bar: the full material name is shown (long names shrink to fit), colour is packed to the right end.
- Print and PDF: choose A4 or A5. A4 = A4 portrait with the sheet at the TOP of the page; the back side goes on page 2, also at the top.
  A5 = A5 landscape as before. Ctrl+P uses the last choice.
- BOTTOMS ORDER SPECIFICATION SHEET has its own layout:
  Bottom Type (default Bottom), Waist Type (default Elastic Waist), Pocket Type (tick one or more, default Side Pockets;
  "No Pockets" clears the others), Leg Opening (default Open Hem - Straight), Additional Custom tick boxes
  (Piping, Contrast Panels, Contrast Stitching, Reflective Trim, Knee Reinforcement Panels, Belt Loops, Side Stripe / Taping).
  Every list is add/editable. After Submit only the ticked options are shown.
  Sizes: one Size Breakdown XS to 4XL (no Adults S/S, L/S or Kids). "+ Other sizes" adds an Optional sizes column where you type
  any other size (5XL, 28, 30...) - the + in its header adds more rows (up to 12).
- Existing settings.json is kept: new lists get their defaults automatically.

Coming: SQLite orders + drafts, native installer.
