namespace Giraffe.Tests.Asserts

module XmlAssert =
    open Xunit
    open System.Linq
    open System.Xml.Linq

    let rec normalize (element : XElement) =
        if element.HasElements then
            XElement(
                element.Name,
                element.Attributes()
                    .Where(fun a -> a.Name.Namespace = XNamespace.Xmlns)
                    .OrderBy(fun a -> a.Name.ToString()),
                element.Elements()
                    .OrderBy(fun a -> a.Name.ToString())
                    .Select(fun e -> normalize(e))
            )
        elif element.IsEmpty then
            XElement(
                element.Name,
                element.Attributes()
                    .OrderBy(fun a -> a.Name.ToString())
              )
         else
            XElement(
                element.Name,
                element.Attributes()
                    .OrderBy(fun a -> a.Name.ToString()), element.Value
               )

    let equals expectedXml actualXml =
        let expectedXElement = XElement.Parse expectedXml |> normalize
        let actualXElement = XElement.Parse actualXml |> normalize
        Assert.Equal(expectedXElement.ToString(), actualXElement.ToString())