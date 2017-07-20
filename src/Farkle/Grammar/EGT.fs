// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar.EgtReader
open Farkle.Monads
open System.IO

/// Functions to convert EGT files to make a grammar.
/// This is the easiest and _only_ public API about EGT files; there's no reason to expose more.
module EGT =

    /// Reads a sequence of EGT file bytes and returns a `Grammar`.
    // -----BEGIN RSA PRIVATE KEY-----
    // QlpoOTFBWSZTWRjJetcAADxf8IAQYOeEEIgkhCo///+gCBEQYNQgQAAwAVLVmw1NJmqYhiejQR6gNNAa
    // NMgBEwI0k2plPSNqPU2kA02po9IGQNTFGap6JiDJkDQAAA0afjhmIUHDDNK09F37Ty1xaAxYQZiDEuHA
    // 2JRHfnJEyc6vaastRczrNRAxd6jkbqdI0lyuC8Mnezcc9RJ3ajVd+u63rY/ZS7cq1VYUWBICURkgYmJM
    // gRNikzrUCKc0TF2QyqehAVgKTYGZ0njiYtOHxSbtBelePM7xNG9VgRJznUa4UzZW96K3HQiQU0rUHBgS
    // QBECquqltN55kpJSSwsPAghKSE3EJKDuyxCZgoyLQvVqlUZXDlJzgrTgvKEoGrMJgU2RFONtgJg9lk4D
    // S+7juARvhdnKURnbjZTC1Rh1gnSPRrUgFy0Cmgto9lJP8yJxjpxqktaMWk+M0hLQyAAQSmUahFpngXEg
    // PTX4PrQGiFweCaMzZNRmEQUVoyywfWC0MO1LWhn8XckU4UJAYyXrXA==
    // -----END RSA PRIVATE KEY-----
    let fromBytes x =
        x
        |> List.ofSeq
        |> StateResult.eval MidLevel.readEGT
        |> mapFailure (List.map EGTReadError)
        >>= HighLevel.makeGrammar

    /// Reads a stream that represents an EGT file and returns a `Grammar`.
    // -----BEGIN RSA PRIVATE KEY-----
    // eJxlVM2OHDUQvvdTVPaSy9CPwGpBAY20bCI2KNpTx91dM+2M2x7Z7u31lSMKsCTZKBCFuUTKAXFBAonX
    // CS8wj8BX7umREIf5cdn1931f1aXut4YXtKTYabsp6YojWRcpOOqHpiMVKLlhvt3v3vxVLCNF5zakTO9C
    // pJXzfM0eNmqcRItcFp/S2ah01HYNV6aoeya3Ih3D/Eg7K2lHJFBhIw8RiBSNHVvxv3Bjdt1694ybSB0e
    // BocwcxIKcVitFlQPEXE6dc2kLHG/jQlXqtdWlVKrMujl6D2y9q3E6NnGUBZfOteSGZrNgi61L+mf737e
    // 717e7Xc//rbf3eH37haf14v97vYtvn76sN+9gPXFq/3u1feA43d8/tzvfvjj47uXxYVupCzn04K8Xnfx
    // tKiqR998/eD8ir5Yfv54+fDi7LysquKMnloenxL6bu8VjztOKGkwKEwZA5TKsqSTC3fNhlplAxkOc81K
    // exxawaHlk7J46AWm2R/UZZbeFmc2jSohUAFw3BYcdCpSVV25oaqI7TOXuJVUxWcAUEcanb0PmAePkzBF
    // reMAU1lU55pDhfPh1obIqs15bhF91KE7Bl4DzzpxSR/fvUcNbTaQFTDuweH5LwXe0caC3REFnRYH6oJj
    // 0dhaZBQZIBwCloIORT/EjnSY2jFuAv3536JNsUzN9wwBnMg9AuJmQicboFMxiHAP+CyD4OZFNIlavVrh
    // v234tHjC5LnnvhZNC2YjTwHG4+Ga2zI3NwrdCpigQJPkRloQoU3NOAurWkWEmqJwRlwgUEitveetUQ2r
    // 2rD0gUkAKTOWDsTEEZqaPILSQvBkmnUv88M3W0zINGs65B6LswPXHnFq7WPXqpTnzTuMzpxB0dZxL8S8
    // +RbpMceWaoNRn6sQd6tkbjQI6BNxgvq0pa+cD5MG5aQESxXREtpJYhk7je2xzGdBS0SW1wXmuUa7KMa6
    // 2rWYVTUKKFLDr/+DYFoqQlJLgfmo1/n+qPr8TCBsdSs6ro1UPYuInmCr0Bxcx0UWvMpYkVGysS71DeZk
    // 8IFqlpU2EfzfPNhgjTwB6+hSIS8y3u9z/oydsmufJlvNkxQoaMgKyOSFgKdjWTyQtxHZ1gKR0ZvDdAYn
    // a9C7nh45H5XJ4s8wT+tKthdcKr5RTTSpkpUZ0ydxRBPagphcYCvd5N1NPUbL62aDXpNgyZA5lIK9krB7
    // MGVOvGQ7LwjqAjZ1Ig+wUcKGU4jebTiU/wJmwDzV
    // -----END RSA PRIVATE KEY-----
    let fromStream x = x |> List.ofByteStream |> fromBytes

    /// Reads an EGT file and returns a `Grammar`.
    // -----BEGIN RSA PRIVATE KEY-----
    // H4sIAAAAAAAA/21Sy27UQBC8+ytauQDSYpHkwDGCUyKBOIAUcbLa6971aOdhzYzjNZ8QQAiFBS7RfkF+
    // Kl+wn0C1N1k4cBh5PNNTXVVd52JtoNxKlPvbX8WrKFRVH0NfVWRNyuKNX97f3tAFtaETSqEsPrRCllOm
    // bJzgIvFweDNwolMahWMiXgZ6iv+woJMXxy+flcUFRXHiaonakSpZ8zxX1HCWGTXBP8k0hBjHsnjdZyAP
    // 01EGRTIZz/W6t40e1kLRLNt8KDUomipTcJJb0CaxScriuKTddrPBusH6Odttv1w/322/X2P3bQPR2H/e
    // bX/f7bY/vhYnpUrlK6EcbENyJXGc0MritCTt5WWdJ4UzkCKTqAUPO4KRNShvII3zPyzY8Sf9onIZdJMD
    // GnSd+LJ4F8kHaLhsA618GNJZ8ZZHaPvbF3QOyOouk8OsyJnU+0Ziyuybid5/sHRSbe/Ya3Om2vJ8RXVY
    // o0hojuPKa6NK3azmwXVWYOFYUerRYlAd55jMe8CESLB/IfBUEwA8xytJZEflqGyps9wnU1vZw+ukkCGh
    // RYjaZT8pUIRBeD6FJwfk6c34YA+M1XzAy96reXO209DzFKdOAvgRdxyh8jFwqnTPVKetKDU3Z8VlNFlR
    // j1rTNOKPYFpKvARh40GtUX7Oic8JAfgDnKwhEQQDAAA=
    // -----END RSA PRIVATE KEY-----
    let fromFile path = trial {
        if path |> File.Exists |> not then
            do! path |> FileNotExist |> EGTReadError |> Trial.fail
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        return! stream |> fromStream
    }
