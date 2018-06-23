// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

/// A type signifying a process made of discrete steps which:
/// * Can be indefinitely interrupted between each step.
/// * After each step, the process returns some feedback to its consumer (such an error log).
/// * The operation can declare herself to be finished and return a final result.
/// * The operation might fail; in this case it will return an adequate error message.
/// The type is modeled after the [Designing with Capabilities](https://fsharpforfunandprofit.com/cap/) presentation.
type LazyFeedbackProcess<'Result,'Feedback,'Error> =
    /// The operation has completed a step.
    /// It can be continued by evaluating the new process that was returned.
    | Continuing of 'Feedback * LazyFeedbackProcess<'Result,'Feedback,'Error> Lazy
    /// The operation failed. It cannot be continued.
    | Failed of 'Error
    /// The operation succeeded. It cannot be continued.
    | Finished of 'Feedback * 'Result