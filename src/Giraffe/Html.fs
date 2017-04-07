module Giraffe.Html

open System
open System.Net

type Attribute = string * string

type Element = string * Attribute[]
/// A Node in Html have the following forms
type Node =
  /// A regular html element that can contain a list of other nodes
  | Element of Element * Node list
  /// A void element is one that can't have content
  /// See: https://www.w3.org/TR/html5/syntax.html#void-elements
  | VoidElement of Element
  /// A text value for a node
  | Text of string
  /// Whitespace for formatting
  | WhiteSpace of string

let tag tag attr (contents : Node list) = Element ((tag, Array.ofList attr), contents)
let voidTag tag attr = VoidElement (tag, Array.ofList attr)
let textContent s = [Text s]
let emptyText = textContent ""

/// Html elements: https://developer.mozilla.org/en-US/docs/Web/HTML/Element

/// Main root
let html = tag "html"

/// Document metadata
let ``base`` = tag "base"
let head = tag "head"
let link attr = voidTag "link" attr
let meta attr = voidTag "meta" attr
let style = tag "style"
let title = tag "title"

/// Content sectioning
let body = tag "body"
let address = tag "address"
let article = tag "article"
let aside = tag "aside"
let footer = tag "footer"
let h1 = tag "h1"
let h2 = tag "h2"
let h3 = tag "h3"
let h4 = tag "h4"
let h5 = tag "h5"
let h6 = tag "h6"
let header = tag "header"
let nav = tag "nav"
let section = tag "section"

/// Text content
let dd = tag "dd"
let div = tag "div"
let dl = tag "dl"
let dt = tag "dt"
let figcaption = tag "figcaption"
let figure = tag "figure"
let hr = voidTag "hr"
let li = tag "li"
let main = tag "main"
let ol = tag "ol"
let p = tag "p"
let pre = tag "pre"
let ul = tag "ul"

/// Inline text semantics
let a = tag "a"
let abbr = tag "abbr"
let b = tag "b"
let bdi = tag "bdi"
let bdo = tag "bdo"
let br = voidTag "br"
let cite = tag "cite"
let code = tag "code"
let data = tag "data"
let dfn = tag "dfn"
let em = tag "em"
let i = tag "i"
let kbd = tag "kbd"
let mark = tag "mark"
let q = tag "q"
let rp = tag "rp"
let rt = tag "rt"
let rtc = tag "rtc"
let ruby = tag "ruby"
let s = tag "s"
let samp = tag "samp"
let small = tag "small"
let span = tag "span"
let strong = tag "strong"
let sub = tag "sub"
let sup = tag "sup"
let time = tag "time"
let u = tag "u"
let var = tag "var"
let wbr = voidTag "wbr"

/// Image and multimedia
let area = voidTag "area"
let audio = tag "audio"
let img = voidTag "img"
let map = tag "map"
let track = voidTag "track"
let video = tag "video"

/// Embedded content
let embed = voidTag "embed"
let object = tag "object"
let param = voidTag "param"
let source = voidTag "source"

/// Scripting
let canvas = tag "canvas"
let noscript = tag "noscript"
let script = tag "script"

/// Demarcating edits
let del = tag "del"
let ins = tag "ins"

/// Table content
let caption = tag "caption"
let col = voidTag "col"
let colgroup = tag "colgroup"
let table = tag "table"
let tbody = tag "tbody"
let td = tag "td"
let tfoot = tag "tfoot"
let th = tag "th"
let thead = tag "thead"
let tr = tag "tr"

/// Forms
let button = tag "button"
let datalist = tag "datalist"
let fieldset = tag "fieldset"
let form = tag "form"
let input = voidTag "input"
let label = tag "label"
let legend = tag "legend"
let meter = tag "meter"
let optgroup = tag "optgroup"
let option = tag "option"
let output = tag "output"
let progress = tag "progress"
let select = tag "select"
let textarea = tag "textarea"

/// Interactive elements
let details = tag "details"
let dialog = tag "dialog"
let menu = tag "menu"
let menuitem = voidTag "menuitem"
let summary = tag "summary"

let samplePage =
  html [] [
    head [] [
        title [] (textContent "Giraffe")
    ]
    body [] [
        div [] [
            h3 [] (textContent "Hello world, from Giraffe!")
        ]
    ]
  ]

/// Rendering

let rec htmlToString node =

  let startElemToString (e, attributes) =
    match attributes with
    | [||] -> sprintf "<%s>" e
    | xs ->
      let attributeString =
        attributes
        |> Array.map (fun (k, v) -> sprintf " %s=\"%s\"" k (WebUtility.HtmlEncode v))
        |> String.Concat
      sprintf "<%s%s>" e attributeString

  let endElemToString (e, _) = sprintf "</%s>" e

  match node with
  | Text text -> text |> WebUtility.HtmlEncode
  | WhiteSpace text -> text
  | Element (e, nodes) ->
    let inner = nodes |> List.map htmlToString |> String.Concat
    let startTag = e |> startElemToString
    let endTag = e |> endElemToString
    sprintf "%s%s%s" startTag inner endTag
  | VoidElement e -> e |> startElemToString

let renderHtmlDocument document =
  sprintf "<!DOCTYPE html>%s%s" (Environment.NewLine) (document |> htmlToString)