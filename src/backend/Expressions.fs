namespace Hobbes.Parsing

open FParsec
open AST

module Expressions = 
    
    let checkBooleanExp  = 
             function 
                 Boolean b -> b
                 | ColumnName c -> ValueOfColumn c
                 | _ -> 
                     //TODO: rewrite to user the FParsec error reporting
                     failwith "Expected a boolean expression"
    

    let formatDate = 
        pipe2 (kwFormat .>>? kwDate >>. columnName .>> spaces1) dateFormat (fun columnName format -> AST.FormatDate(columnName,format))
    

    let private opp = new OperatorPrecedenceParser<AST.ComputationExpression,unit,unit>()
    let private expr = opp.ExpressionParser
    
    let expressionInBrackets = 
        spaces >>. (between (skipString "[") (skipString "]") expr) .>> spaces

    let private computationExpression =
        // we set up an operator precedence parser for parsing the arithmetic expressions
        
        //moving mean 3 [expr]
        let moving = 
            kwMoving >>? (pipe3 (reduction)  (spaces1 >>. pint32) expressionInBrackets (fun reduction windowSize columnExpression ->
               AST.Moving(reduction, windowSize, columnExpression) |> ColumnExpression
            ))

        //expanding sum [expr]
        let expanding = 
            pipe2  (kwExpanding >>? reduction) expressionInBrackets (fun reduction columnExpression ->
                AST.Expanding(reduction,columnExpression) |> ColumnExpression
            )   
        let regression = 
            pipe3 (kwLinear .>>? kwRegression)
                  expressionInBrackets 
                  expressionInBrackets
                  (fun regressionType inputs outputs -> AST.Regression(regressionType,inputs,outputs)) 
        let ``int`` = kwInt >>? expr >>= (AST.Int >> preturn) 
        let ifThisThenElse =
            (pipe3 (kwIf >>? between (skipString "[") (skipString "]") expr .>> spaces1)
                  (between (skipString "{") (skipString "}") expr .>> spaces1)
                  (kwElse >>? between (skipString "{") (skipString "}") expr)
                  (fun condition thenBody elseBody -> 
                      let condition = 
                          checkBooleanExp condition
                      AST.IfThisThenElse(condition,thenBody,elseBody)
                  ))
            
        opp.TermParser <- 
            ifThisThenElse
            <|> int
            <|> moving
            <|> expanding
            <|> regression
            <|> (pnumber >>= (AST.Number >> preturn))
            <|> kwMissing
            <|> kwKeys
            <|> formatDate
            <|> quotedStringLiteral .>> spaces
            <|> (columnName .>> spaces >>= (AST.ColumnName >> preturn))
            <|> between (stringThenWhiteSpace "(") (stringThenWhiteSpace ")") expr
            

        // operator definitions follow the schema
        // operator type, string, trailing whitespace parser, precedence, associativity, function to apply
        let exp op lhs rhs = 
            AST.Binary(lhs,rhs,op)
        let comp op lhs rhs = 
            AST.Comparison(lhs,rhs,op) |> AST.Boolean
        
        let andOr op lhs rhs = 
            let l = checkBooleanExp lhs
            let r = checkBooleanExp rhs
            op(l,r) |> AST.Boolean
        ((PrefixOperator("!", spaces, 5, true, (fun e -> 
              let exp = checkBooleanExp e
              exp 
              |> AST.Not
              |> AST.Boolean)
         ) :> Operator<_,_,_>)
        :: [
                InfixOperator("+", spaces, 3, Associativity.Left, exp AST.Addition)
                InfixOperator("-", spaces, 3, Associativity.Left, exp AST.Subtraction)
                InfixOperator("*", spaces, 4, Associativity.Left, exp AST.Multiplication)
                InfixOperator("/", spaces, 4, Associativity.Left, exp AST.Division)

                InfixOperator("&&", spaces, 1, Associativity.Left, (andOr AST.And))
                InfixOperator("||", spaces, 1, Associativity.Left, (andOr AST.Or))

                InfixOperator("contains", spaces1, 1, Associativity.Left, comp AST.Contains)
                InfixOperator(">",  spaces, 2, Associativity.Left, comp AST.GreaterThan)
                InfixOperator(">=", spaces, 2, Associativity.Left, comp AST.GreaterThanOrEqual)
                InfixOperator("<=", spaces, 2, Associativity.Left, comp AST.LessThanOrEqual)
                InfixOperator("<",  spaces, 2, Associativity.Left, comp AST.LessThan)
                InfixOperator("=",  spaces, 2, Associativity.Left, comp AST.EqualTo)
                
            ]) |> List.iter(opp.AddOperator)
                                                             
        //opp.AddOperator(InfixOperator("^", ws, 3, Associativity.Right, fun x y -> System.Math.Pow(x, y)))
        //opp.AddOperator(PrefixOperator("-", ws, 4, true, fun x -> -x))
        spaces >>. expr
        
        
    let expression =
        computationExpression