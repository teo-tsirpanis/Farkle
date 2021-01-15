// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

type HtmlFunctions(options) =
    member _.custom_head = options.CustomHeadContent
    static member attr_escape x = System.Web.HttpUtility.HtmlAttributeEncode x
