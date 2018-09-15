// ---------------------------
// Attribution to original authors of this code
// ---------------------------
// This code has been originally ported from Suave with small modifications afterwards.
//
// The original code has been authored by
// * Henrik Feldt (https://github.com/haf)
// * Ademar Gonzalez (https://github.com/ademar)
//
// You can find the original implementation here:
// https://github.com/SuaveIO/suave/blob/master/src/Experimental/Html.fs
//
// Thanks to Suave (https://github.com/SuaveIO/suave) for letting us borrow their code
// and thanks to Florian Verdonck (https://github.com/nojaf) for porting it to Giraffe.

module Giraffe.GiraffeViewEngine

open System
open System.Net
open System.Text

// ---------------------------
// Definition of different HTML content
//
// For more info check:
// - https://developer.mozilla.org/en-US/docs/Web/HTML/Element
// - https://www.w3.org/TR/html5/syntax.html#void-elements
// ---------------------------

type XmlAttribute =
    | KeyValue of string * string
    | Boolean  of string

type XmlElement   = string * XmlAttribute[]    // Name * XML attributes

type XmlNode =
    | ParentNode  of XmlElement * XmlNode list // An XML element which contains nested XML elements
    | VoidElement of XmlElement                // An XML element which cannot contain nested XML (e.g. <hr /> or <br />)
    | Text        of string                    // Text content

// ---------------------------
// Helper functions
// ---------------------------

let inline private encode v = WebUtility.HtmlEncode v

// ---------------------------
// Building blocks
// ---------------------------

let attr (key : string) (value : string) = KeyValue (key, encode value)
let flag (key : string) = Boolean key

let tag (tagName    : string)
        (attributes : XmlAttribute list)
        (contents   : XmlNode list) =
    ParentNode ((tagName, Array.ofList attributes), contents)

let voidTag (tagName    : string)
            (attributes : XmlAttribute list) =
    VoidElement (tagName, Array.ofList attributes)

let encodedText (content : string) = Text (encode content)
let rawText     (content : string) = Text content
let emptyText                      = rawText ""
let comment     (content : string) = rawText (sprintf "<!-- %s -->" content)

// ---------------------------
// Default HTML elements
// ---------------------------

// Main root
let html       = tag "html"

// Document metadata
let ``base``   = voidTag "base"
let head       = tag "head"
let link attr  = voidTag "link" attr
let meta attr  = voidTag "meta" attr
let style      = tag "style"
let title      = tag "title"

// Content sectioning
let blockquote = tag "blockquote"
let body       = tag "body"
let address    = tag "address"
let article    = tag "article"
let aside      = tag "aside"
let footer     = tag "footer"
let hgroup     = tag "hgroup"
let h1         = tag "h1"
let h2         = tag "h2"
let h3         = tag "h3"
let h4         = tag "h4"
let h5         = tag "h5"
let h6         = tag "h6"
let header     = tag "header"
let nav        = tag "nav"
let section    = tag "section"

// Text content
let dd         = tag "dd"
let div        = tag "div"
let dl         = tag "dl"
let dt         = tag "dt"
let figcaption = tag "figcaption"
let figure     = tag "figure"
let hr         = voidTag "hr"
let li         = tag "li"
let main       = tag "main"
let ol         = tag "ol"
let p          = tag "p"
let pre        = tag "pre"
let ul         = tag "ul"

// Inline text semantics
let a          = tag "a"
let abbr       = tag "abbr"
let b          = tag "b"
let bdi        = tag "bdi"
let bdo        = tag "bdo"
let br         = voidTag "br"
let cite       = tag "cite"
let code       = tag "code"
let data       = tag "data"
let dfn        = tag "dfn"
let em         = tag "em"
let i          = tag "i"
let kbd        = tag "kbd"
let mark       = tag "mark"
let q          = tag "q"
let rp         = tag "rp"
let rt         = tag "rt"
let rtc        = tag "rtc"
let ruby       = tag "ruby"
let s          = tag "s"
let samp       = tag "samp"
let small      = tag "small"
let span       = tag "span"
let strong     = tag "strong"
let sub        = tag "sub"
let sup        = tag "sup"
let time       = tag "time"
let u          = tag "u"
let var        = tag "var"
let wbr        = voidTag "wbr"

// Image and multimedia
let area       = voidTag "area"
let audio      = tag "audio"
let img        = voidTag "img"
let map        = tag "map"
let track      = voidTag "track"
let video      = tag "video"

// Embedded content
let embed      = voidTag "embed"
let object     = tag "object"
let param      = voidTag "param"
let source     = voidTag "source"

// Scripting
let canvas     = tag "canvas"
let noscript   = tag "noscript"
let script     = tag "script"

// Demarcating edits
let del        = tag "del"
let ins        = tag "ins"

// Table content
let caption    = tag "caption"
let col        = voidTag "col"
let colgroup   = tag "colgroup"
let table      = tag "table"
let tbody      = tag "tbody"
let td         = tag "td"
let tfoot      = tag "tfoot"
let th         = tag "th"
let thead      = tag "thead"
let tr         = tag "tr"

// Forms
let button     = tag "button"
let datalist   = tag "datalist"
let fieldset   = tag "fieldset"
let form       = tag "form"
let input      = voidTag "input"
let label      = tag "label"
let legend     = tag "legend"
let meter      = tag "meter"
let optgroup   = tag "optgroup"
let option     = tag "option"
let output     = tag "output"
let progress   = tag "progress"
let select     = tag "select"
let textarea   = tag "textarea"

// Interactive elements
let details    = tag "details"
let dialog     = tag "dialog"
let menu       = tag "menu"
let menuitem   = voidTag "menuitem"
let summary    = tag "summary"

// ---------------------------
// Default HTML attributes
// ---------------------------

[<AutoOpen>]
module Attributes =
    // https://www.w3.org/TR/html5/index.html#attributes-1
    let _abbr               = attr "abbr"
    let _accept             = attr "accept"
    let _acceptCharset      = attr "accept-charset"
    let _accesskey          = attr "accesskey"
    let _action             = attr "action"
    let _alt                = attr "alt"
    let _autocomplete       = attr "autocomplete"
    let _border             = attr "border"
    let _challenge          = attr "challenge"
    let _charset            = attr "charset"
    let _cite               = attr "cite"
    let _class              = attr "class"
    let _cols               = attr "cols"
    let _colspan            = attr "colspan"
    let _content            = attr "content"
    let _contenteditable    = attr "contenteditable"
    let _coords             = attr "coords"
    let _crossorigin        = attr "crossorigin"
    let _data               = attr "data"
    let _datetime           = attr "datetime"
    let _dir                = attr "dir"
    let _dirname            = attr "dirname"
    let _download           = attr "download"
    let _enctype            = attr "enctype"
    let _for                = attr "for"
    let _form               = attr "form"
    let _formaction         = attr "formaction"
    let _formenctype        = attr "formenctype"
    let _formmethod         = attr "formmethod"
    let _formtarget         = attr "formtarget"
    let _headers            = attr "headers"
    let _height             = attr "height"
    let _high               = attr "high"
    let _href               = attr "href"
    let _hreflang           = attr "hreflang"
    let _httpEquiv          = attr "http-equiv"
    let _id                 = attr "id"
    let _integrity          = attr "integrity"
    let _keytype            = attr "keytype"
    let _kind               = attr "kind"
    let _label              = attr "label"
    let _lang               = attr "lang"
    let _list               = attr "list"
    let _low                = attr "low"
    let _manifest           = attr "manifest"
    let _max                = attr "max"
    let _maxlength          = attr "maxlength"
    let _media              = attr "media"
    let _mediagroup         = attr "mediagroup"
    let _method             = attr "method"
    let _min                = attr "min"
    let _minlength          = attr "minlength"
    let _name               = attr "name"
    let _optimum            = attr "optimum"
    let _pattern            = attr "pattern"
    let _placeholder        = attr "placeholder"
    let _poster             = attr "poster"
    let _preload            = attr "preload"
    let _rel                = attr "rel"
    let _rows               = attr "rows"
    let _rowspan            = attr "rowspan"
    let _sandbox            = attr "sandbox"
    let _spellcheck         = attr "spellcheck"
    let _scope              = attr "scope"
    let _shape              = attr "shape"
    let _size               = attr "size"
    let _sizes              = attr "sizes"
    let _span               = attr "span"
    let _src                = attr "src"
    let _srcdoc             = attr "srcdoc"
    let _srclang            = attr "srclang"
    let _start              = attr "start"
    let _step               = attr "step"
    let _style              = attr "style"
    let _tabindex           = attr "tabindex"
    let _target             = attr "target"
    let _title              = attr "title"
    let _translate          = attr "translate"
    let _type               = attr "type"
    let _usemap             = attr "usemap"
    let _value              = attr "value"
    let _width              = attr "width"
    let _wrap               = attr "wrap"

    // Mouse events
    // https://www.w3schools.com/jsref/obj_mouseevent.asp
    let _onclick            = attr "onclick"
    let _oncontextmenu      = attr "oncontextmenu"
    let _ondblclick         = attr "ondblclick"
    let _onmousedown        = attr "onmousedown"
    let _onmouseenter       = attr "onmouseenter"
    let _onmouseleave       = attr "onmouseleave"
    let _onmousemove        = attr "onmousemove"
    let _onmouseout         = attr "onmouseout"
    let _onmouseover        = attr "onmouseover"
    let _onmouseup          = attr "onmouseup"

    // Touch events
    // https://www.w3schools.com/jsref/obj_touchevent.asp
    let _ontouchcancel      = attr "ontouchcancel"
    let _ontouchend         = attr "ontouchend"
    let _ontouchmove        = attr "ontouchmove"
    let _ontouchstart       = attr "ontouchstart"

    // Keyboard events
    // https://www.w3schools.com/jsref/obj_keyboardevent.asp
    let _onkeydown          = attr "onkeydown"
    let _onkeypress         = attr "onkeypress"
    let _onkeyup            = attr "onkeyup"

    // Drag and drop events
    // https://www.w3schools.com/jsref/obj_dragevent.asp
    let _ondrag             = attr "ondrag"
    let _ondragend          = attr "ondragend"
    let _ondragenter        = attr "ondragenter"
    let _ondragleave        = attr "ondragleave"
    let _ondragover         = attr "ondragover"
    let _ondragstart        = attr "ondragstart"
    let _ondrop             = attr "ondrop"

    // Focus events
    // https://www.w3schools.com/jsref/obj_focusevent.asp
    let _onblur              = attr "onblur"
    let _onfocus             = attr "onfocus"
    let _onfocusin           = attr "onfocusin"
    let _onfocusout          = attr "onfocusout"

    // Input events
    // https://www.w3schools.com/jsref/obj_inputevent.asp
    let _oninput             = attr "oninput"

    // Mouse wheel events
    // https://www.w3schools.com/jsref/obj_wheelevent.asp
    let _onwheel            = attr "onwheel"

    // Flags
    let _async              = flag "async"
    let _autofocus          = flag "autofocus"
    let _autoplay           = flag "autoplay"
    let _checked            = flag "checked"
    let _controls           = flag "controls"
    let _default            = flag "default"
    let _defer              = flag "defer"
    let _disabled           = flag "disabled"
    let _formnovalidate     = flag "formnovalidate"
    let _hidden             = flag "hidden"
    let _ismap              = flag "ismap"
    let _loop               = flag "loop"
    let _multiple           = flag "multiple"
    let _muted              = flag "muted"
    let _novalidate         = flag "novalidate"
    let _readonly           = flag "readonly"
    let _required           = flag "required"
    let _reversed           = flag "reversed"
    let _scoped             = flag "scoped"
    let _selected           = flag "selected"
    let _typemustmatch      = flag "typemustmatch"


/// Attributes to support WAI-ARIA accessibility guidelines
module Accessibility =

    // Valid role attributes
    // (obtained from https://www.w3.org/TR/wai-aria/#role_definitions)
    let _roleAlert            = attr "role" "alert"
    let _roleAlertDialog      = attr "role" "alertdialog"
    let _roleApplication      = attr "role" "application"
    let _roleArticle          = attr "role" "article"
    let _roleBanner           = attr "role" "banner"
    let _roleButton           = attr "role" "button"
    let _roleCell             = attr "role" "cell"
    let _roleCheckBox         = attr "role" "checkbox"
    let _roleColumnHeader     = attr "role" "columnheader"
    let _roleComboBox         = attr "role" "combobox"
    let _roleComplementary    = attr "role" "complementary"
    let _roleContentInfo      = attr "role" "contentinfo"
    let _roleDefinition       = attr "role" "definition"
    let _roleDialog           = attr "role" "dialog"
    let _roleDirectory        = attr "role" "directory"
    let _roleDocument         = attr "role" "document"
    let _roleFeed             = attr "role" "feed"
    let _roleFigure           = attr "role" "figure"
    let _roleForm             = attr "role" "form"
    let _roleGrid             = attr "role" "grid"
    let _roleGridCell         = attr "role" "gridcell"
    let _roleGroup            = attr "role" "group"
    let _roleHeading          = attr "role" "heading"
    let _roleImg              = attr "role" "img"
    let _roleLink             = attr "role" "link"
    let _roleList             = attr "role" "list"
    let _roleListBox          = attr "role" "listbox"
    let _roleListItem         = attr "role" "listitem"
    let _roleLog              = attr "role" "log"
    let _roleMain             = attr "role" "main"
    let _roleMarquee          = attr "role" "marquee"
    let _roleMath             = attr "role" "math"
    let _roleMenuBar          = attr "role" "menubar"
    let _roleMenuItem         = attr "role" "menuitem"
    let _roleMenuItemCheckBox = attr "role" "menuitemcheckbox"
    let _roleMenuItemRadio    = attr "role" "menuitemradio"
    let _roleNavigation       = attr "role" "navigation"
    let _roleNone             = attr "role" "none"
    let _roleNote             = attr "role" "note"
    let _roleOption           = attr "role" "option"
    let _rolePresentation     = attr "role" "presentation"
    let _roleProgressBar      = attr "role" "progressbar"
    let _roleRadio            = attr "role" "radio"
    let _roleRadioGroup       = attr "role" "radiogroup"
    let _roleRegion           = attr "role" "region"
    let _roleRow              = attr "role" "row"
    let _roleRowGroup         = attr "role" "rowgroup"
    let _roleRowHeader        = attr "role" "rowheader"
    let _roleScrollBar        = attr "role" "scrollbar"
    let _roleSearch           = attr "role" "search"
    let _roleSearchBox        = attr "role" "searchbox"
    let _roleSeparator        = attr "role" "separator"
    let _roleSlider           = attr "role" "slider"
    let _roleSpinButton       = attr "role" "spinbutton"
    let _roleStatus           = attr "role" "status"
    let _roleSwitch           = attr "role" "switch"
    let _roleTab              = attr "role" "tab"
    let _roleTable            = attr "role" "table"
    let _roleTabList          = attr "role" "tablist"
    let _roleTabPanel         = attr "role" "tabpanel"
    let _roleTerm             = attr "role" "term"
    let _roleTextBox          = attr "role" "textbox"
    let _roleTimer            = attr "role" "timer"
    let _roleToolBar          = attr "role" "toolbar"
    let _roleToolTip          = attr "role" "tooltip"
    let _roleTree             = attr "role" "tree"
    let _roleTreeGrid         = attr "role" "treegrid"
    let _roleTreeItem         = attr "role" "treeitem"

    // Valid aria attributes
    // (obtained from https://www.w3.org/TR/wai-aria/#state_prop_def)
    let _ariaActiveDescendant = attr "aria-activedescendant"
    let _ariaAtomic           = attr "aria-atomic"
    let _ariaAutocomplete     = attr "aria-autocomplete"
    let _ariaBusy             = attr "aria-busy"
    let _ariaChecked          = attr "aria-checked"
    let _ariaColCount         = attr "aria-colcount"
    let _ariaColIndex         = attr "aria-colindex"
    let _ariaColSpan          = attr "aria-colspan"
    let _ariaControls         = attr "aria-controls"
    let _ariaCurrent          = attr "aria-current"
    let _ariaDescribedBy      = attr "aria-describedby"
    let _ariaDetails          = attr "aria-details"
    let _ariaDisabled         = attr "aria-disabled"
    let _ariaDropEffect       = attr "aria-dropeffect"
    let _ariaErrorMessage     = attr "aria-errormessage"
    let _ariaExpanded         = attr "aria-expanded"
    let _ariaFlowTo           = attr "aria-flowto"
    let _ariaGrabbed          = attr "aria-grabbed"
    let _ariaHasPopup         = attr "aria-haspopup"
    let _ariaHidden           = attr "aria-hidden"
    let _ariaInvalid          = attr "aria-invalid"
    let _ariaKeyShortcuts     = attr "aria-keyshortcuts"
    let _ariaLabel            = attr "aria-label"
    let _ariaLabelledBy       = attr "aria-labeledby"
    let _ariaLevel            = attr "aria-level"
    let _ariaLive             = attr "aria-live"
    let _ariaModal            = attr "aria-modal"
    let _ariaMultiline        = attr "aria-multiline"
    let _ariaMultiSelectable  = attr "aria-multiselectable"
    let _ariaOrientation      = attr "aria-orientation"
    let _ariaOwns             = attr "aria-owns"
    let _ariaPlaceholder      = attr "aria-placeholder"
    let _ariaPosInset         = attr "aria-posinset"
    let _ariaPressed          = attr "aria-pressed"
    let _ariaReadOnly         = attr "aria-readonly"
    let _ariaRelevant         = attr "aria-relevant"
    let _ariaRequired         = attr "aria-required"
    let _ariaRoleDescription  = attr "aria-roledescription"
    let _ariaRowCount         = attr "aria-rowcount"
    let _ariaRowIndex         = attr "aria-rowindex"
    let _ariaRowSpan          = attr "aria-rowspan"
    let _ariaSelected         = attr "aria-selected"
    let _ariaSetSize          = attr "aria-setsize"
    let _ariaSort             = attr "aria-sort"
    let _ariaValueMax         = attr "aria-valuemax"
    let _ariaValueMin         = attr "aria-valuemin"
    let _ariaValueNow         = attr "aria-valuenow"
    let _ariaValueText        = attr "aria-valuetext"

// ---------------------------
// Build HTML/XML views
// ---------------------------

[<RequireQualifiedAccess>]
module ViewBuilder =
    let inline private (+=) (sb : StringBuilder) (text : string) = sb.Append(text)
    let inline private (+!) (sb : StringBuilder) (text : string) = sb.Append(text) |> ignore

    let inline private selfClosingBracket (isHtml : bool) =
        if isHtml then ">" else " />"

    let rec private buildNode (isHtml : bool) (sb : StringBuilder) (node : XmlNode) : unit =

        let buildElement closingBracket (elemName, attributes : XmlAttribute array) =
            match attributes with
            | [||] -> do sb += "<" += elemName +! closingBracket
            | _    ->
                do sb += "<" +! elemName

                attributes
                |> Array.iter (fun attr ->
                    match attr with
                    | KeyValue (k, v) -> do sb += " " += k += "=\"" += v +! "\""
                    | Boolean k       -> do sb += " " +! k)

                do sb +! closingBracket

        let inline buildParentNode (elemName, attributes : XmlAttribute array) (nodes : XmlNode list) =
            do buildElement ">" (elemName, attributes)
            for node in nodes do buildNode isHtml sb node
            do sb += "</" += elemName +! ">"

        match node with
        | Text text             -> do sb +! text
        | ParentNode (e, nodes) -> do buildParentNode e nodes
        | VoidElement e         -> do buildElement (selfClosingBracket isHtml) e

    let buildXmlNode  = buildNode false
    let buildHtmlNode = buildNode true

    let buildXmlNodes  sb (nodes : XmlNode list) = for n in nodes do buildXmlNode sb n
    let buildHtmlNodes sb (nodes : XmlNode list) = for n in nodes do buildHtmlNode sb n

    let buildHtmlDocument sb (document : XmlNode) =
        sb += "<!DOCTYPE html>" +! Environment.NewLine
        buildHtmlNode sb document

// ---------------------------
// Render HTML/XML views
// ---------------------------

let renderXmlNode (node : XmlNode) : string =
    let sb = new StringBuilder() in ViewBuilder.buildXmlNode sb node
    sb.ToString()

let renderXmlNodes (nodes : XmlNode list) : string =
    let sb = new StringBuilder() in ViewBuilder.buildXmlNodes sb nodes
    sb.ToString()

let renderHtmlNode (node : XmlNode) : string =
    let sb = new StringBuilder() in ViewBuilder.buildHtmlNode sb node
    sb.ToString()

let renderHtmlNodes (nodes : XmlNode list) : string =
    let sb = new StringBuilder() in ViewBuilder.buildHtmlNodes sb nodes
    sb.ToString()

let renderHtmlDocument (document : XmlNode) : string =
    let sb = new StringBuilder() in ViewBuilder.buildHtmlDocument sb document
    sb.ToString()