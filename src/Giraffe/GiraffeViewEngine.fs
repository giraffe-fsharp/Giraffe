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
    | EncodedText of string                    // XML encoded text content
    | RawText     of string                    // Raw text content

// ---------------------------
// Building blocks
// ---------------------------

let attr (key : string) (value : string) = KeyValue (key, value)
let flag (key : string) = Boolean key

let tag (tagName    : string)
        (attributes : XmlAttribute list)
        (contents   : XmlNode list) =
    ParentNode ((tagName, Array.ofList attributes), contents)

let voidTag (tagName    : string)
            (attributes : XmlAttribute list) =
    VoidElement (tagName, Array.ofList attributes)

let encodedText (content : string) = EncodedText content
let rawText     (content : string) = RawText content
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
    let _selected           = flag "selected"
    let _typemustmatch      = flag "typemustmatch"

// ---------------------------
// Render XML string
// ---------------------------

let rec private nodeToString (htmlStyle : bool) (node : XmlNode) =
    let startElementToString selfClosing (elemName, attributes : XmlAttribute array) =
        let closingBracket =
            match selfClosing with
            | false -> ">"
            | true ->
                match htmlStyle with
                | false -> " />"
                | true  -> ">"
        match attributes with
        | [||] -> sprintf "<%s%s" elemName closingBracket
        | _    ->
            attributes
            |> Array.map (fun attr ->
                match attr with
                | KeyValue (k, v) -> sprintf " %s=\"%s\"" k (WebUtility.HtmlEncode v)
                | Boolean k       -> sprintf " %s" k)
            |> String.Concat
            |> sprintf "<%s%s%s" elemName
            <| closingBracket

    let endElementToString (elemName, _) = sprintf "</%s>" elemName

    let parentNodeToString (elem : XmlElement, nodes : XmlNode list) =
        let innerContent = nodes |> List.map (nodeToString htmlStyle) |> String.Concat
        let startTag     = elem  |> startElementToString false
        let endTag       = elem  |> endElementToString
        sprintf "%s%s%s" startTag innerContent endTag

    match node with
    | EncodedText text      -> WebUtility.HtmlEncode text
    | RawText text          -> text
    | ParentNode (e, nodes) -> parentNodeToString (e, nodes)
    | VoidElement e         -> startElementToString true e

let renderXmlNode = nodeToString false

let renderXmlNodes (nodes : XmlNode list) =
    nodes
    |> List.map renderXmlNode
    |> String.Concat

let renderHtmlNode = nodeToString true

let renderHtmlNodes (nodes : XmlNode list) =
    nodes
    |> List.map renderHtmlNode
    |> String.Concat

let renderHtmlDocument (document : XmlNode) =
    document
    |> renderHtmlNode
    |> sprintf "<!DOCTYPE html>%s%s" Environment.NewLine