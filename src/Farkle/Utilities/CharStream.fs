// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System

type CharStreamIndex = private CharStreamIndex of uint64
with
    member x.Index = match x with CharStreamIndex x -> x

type Position = {
    Line: uint64
    Column: uint64
    Index: CharStreamIndex
}

type CharSpan = private CharSpan of start: uint64 * length: int

type private StaticBlock = {
    Stream: ReadOnlyMemory<char>
    mutable Position: Position
}

type CharStream = private StaticBlock of StaticBlock
with
    member x.Position = match x with StaticBlock sb -> sb.Position

type CharStreamViewer = private CharStreamViewer of stream: CharStream * idx: CharStreamIndex
with
    member x.Index = match x with | CharStreamViewer (_, idx) -> idx

module CharStream =

    let view cs =
        match cs with
        | StaticBlock sb -> CharStreamViewer (cs, cs.Position.Index)
    let (|CSCons|CSNil|) (CharStreamViewer (cs, CharStreamIndex idx)) =
        match cs with
        | StaticBlock sb when idx < uint64 sb.Stream.Length -> CSCons(sb.Stream.Span.[int idx], CharStreamViewer(cs, CharStreamIndex <| idx + 1UL))
        | StaticBlock _ -> CSNil

    let pinSpan cs (CharStreamIndex idxTo) =
        match cs with
        | StaticBlock {Stream = b; Position = {Index = CharStreamIndex idxFrom}} when
            idxFrom < idxTo && idxTo < uint64 b.Length && idxTo - idxFrom <= uint64 Int32.MaxValue ->
            Some <| CharSpan (idxFrom, int <| idxTo - idxFrom)
        | StaticBlock _ -> None

    let appendSpan (CharSpan (start, length)) l = CharSpan (start, length + l)

    let consume (CharSpan)