(*** hide ***)
#r "bin/debug/netcoreapp3.1/hobbes.workbench.dll"
#r "bin/debug/netcoreapp3.1/hobbes.core.dll"
#r "bin/debug/netcoreapp3.1/hobbes.web.dll"
open Hobbes.DSL
open Hobbes.Parsing.AST
open Workbench.Types
open Workbench.Transformations.General
open Workbench.Configurations.State

(**
# Tutorial on how to write transformations for Hobbes
This tutorial will go through each of the commands that hobbes is capable of,
explain what they do and how to use them. It will also do a quick run through of some syntax rules that could be useful.
Finally it will describe how to write a full transformation, and how to group those in configurations that can be used
to actually get the transformed data.
*)

(** 
## Commands
#### Renaming
Lets start with a simple command, namely the rename command. It simply renames a column. Here is an example:
*)
rename "State" "DetailedState"

(**
This command is very simply. It looks for a column named 'State', and renames that column to 'DetailedState'

As the data at the point of your transformation will be uniform, many of the columns has been coded as a type that can be used in F#.
This can be used to avoid spelling mistakes as these will be caught by the compiler. Below is the same example as before but
using one of these types
*)
rename State.Name "DetailedState"
(**
A complete list of all the columns where this type is provided can be seen below.
*)
SprintNumber
SprintName
SprintStartDate
SprintEndDate
WorkItemId
WorkItemType
Team
Estimate
ChangedDate
State

(**
#### Sort by
The sort by command sorts the rows in order of a specific column, that is given as an argument. Lets look at an example.
*)
sort by SprintNumber.Name
(**
This example sorts the rows by the SprintNumber column, so that we would get the rows ordered by sprints.
*)

(**
#### Dense
The dense command is used to filter either rows or columns. 
Only dense rows/columns will be kept. A dense row/column is one with out a lot of missing values.
The following two examples shows how to do both respectively.
*)
dense rows

dense columns

(**
#### Slice
The slice command is used to keep only some columns or some rows. Let us look at an example
*)
slice columns [SprintNumber.Name; "SomeOtherColumn"]
(**
This example would remove all other columns except for SprintNumber and SomeOtherColumn. As can be seen it takes as its first argument
wether it should work on columns or rows. If you instead wanted to slice the rows, you would write 'rows' instead of 'columns'. The
second argument is the list of columns you want to remove.. !!!I DONT KNOW HOW YOU WOULD SPECIFY THE ROWS!!!
*)

(**
#### Create
The create command is used to create a new column. It expects as arguments the name of the column, and an expression that determines
what each row of the column should contain. Lets take a look at a simple example.
*)
create "IsAboveSprintFour" (If ((SprintNumber.Expression) .> 4)
                               (Then (!!> "True"))
                               (Else (!!> "False"))
                           )
(**
This example creates a new column that has the value true or false depending on wether the sprint number of that row is greater than 4
It introduces a couple of new keywords so lets go through them one by one.
The 'If' keyword is a simple if statement. It expects a boolean expression, (which is an expression that evaluates to either true or false),
and a 'Then' branch and a 'Else' branch. If the boolean expression is true then the 'Then' branch is executed. Otherwise it will execute
the 'Else' branch. In this case each of the branches just returns some text to put in the row, but they could be more complicated.
The '!!>' operator turns the text into an expression that the language can understand and use. If we take a look at the boolean
expression, we see the operator '.>'. This means greater than. It takes two expressions, and returns true if the first expression is 
larger than the second. The first expression we give to '.>' is the SprintNumber column. But instead of writing 'SprintNumber.Name',
we write 'SprintNumber.Expression' because we need the text as an expression and not just a string. The second argument we give is
the number 4.

This whole transformation will create a new column named IsAboveSprintFour and will either have the value 'True' if the corresponding
SprintNumber is above 4, or false if it is 4 or lower.

'.>' isn't the only comparison operator in the language. '==' is the equal operator, '!=' is the not equal operator. There also
exists the 'or' '.||' and 'and' '.&&' operators to combine boolean expressions. Lastly the keyword 'Not' is used to invert a
boolean expression.


Lets continue using the create command as an example, to go through some other keywords that can be used in the boolean expression.
Take a look at the next example
*)
create "IsSprint4or5" (If (contains (!> "SprintNumber") 
                            [
                            (NumberConstant 4.0)
                            (NumberConstant 5.0)
                            ]
                          )
                            (Then (!!> "True"))
                            (Then (!!> "False"))
                      )
(**
The above example will create a column, that has the value 'True' or 'False' depending on wether the 'SprintNumber' column
contains the number 4 or 5. Here we have used the keyword contains which expects a column name and a list of expressions.
In the previous example we wrote 'SprintNumber.Expression' but here we've instead written '!> 'SprintNumber'. These are equivalent.
Both '!>' and '!!>' turns the string into an expression, but the difference is that the first turns it into an identifier
(Which identifies a column) and the second just turns it into a string literal. The second argument to contains is a list of values
to check wether each row of "SprintNumber" contains one of them. As with the strings, the numbers also have to be turned into 
expressions, and therefore we write 'NumberConstant' in front of them.
*)

(**
It is of course also possible to use create without using an if-statement. Lets look at two examples.
*)
create (column "Burn up") (expanding Sum (!> "Done")) 
create (column "Velocity") ((moving Mean 3 (!> "Done")))
(**
The first example creates a new column called 'Burn up'. It fills the rows of the column by doing an expanding sum of the
'Done' column. Meaning a running total of values in the 'Done' column.

The second example creates a new column called 'Velocity'. It fills the rows of the column by taking a moving mean of the
'Done' column, with a window of 3. 
*)

(**
#### Only
Only is a command that takes a boolean expression, and then only includes the rows that satisfy that expression.
Let's look at an example
*)
only (isntMissing SprintNumber.Expression)
(**
This example will remove all the rows, where the SprintNumber column doesn't have a value. The 'instMissing' keyword
expects an identifier for a column, and will be true, for all rows, where that column has a value. It also has the inverse 'isMissing'
which does the opposite.
*)

(**
#### Pivot
Pivot is a command that pivots the data matrix. It takes as it's first argument, the column that should be the row key.
Second arguments is the column that should be the column key. And finally it takes a 'reduction' and which column to reduce.
Lets look at an example to understand.
*)
pivot SprintNumber.Expression (!> "SimpleState") Count WorkItemId.Expression
(**
This example would count the number of each WorkItemId pr. sprint. In this example the 'reduction' is 'Count'
The language supports other kinds of reductions.
*)
Sum      //Sums the values
Median   //Takes the median of the values
Mean     //Takes the mean of the values
StdDev   //Takes the standard deviation of the values
Variance //Takes the variance of the values
Max      //Takes the maximum value
Min      //Takes the minimum value
(**
All of these could be substituted for the 'Count' keyword in the previous example.
*)

(**
#### Group by
The group by command is best explained by first looking at an example.
*)
group by [SprintName.Name; WorkItemId.Name] => 
         (maxby ChangedDate.Expression)
(**
This example groups the data by the SprintName column, and then by the WorkItemId column. This would mean that there would be a
row for each unique WorkItemId per SprintName. As there could be multiple rows for each of these we either need a selector
or a reduction. The reductions were explained earlier, fx. sum, which would sum the values of the rows of each
grouping. Here we have instead used a selector. Namely the 'maxby' selector, which simply chooses the row with the maximum value.
Regardless of wether we use a selector or a reduction, we need to tell the group by command, what column it should work on.
In this case it works on the ChangedDate column, which would mean we would chose the row, where ChangedDate is the maximum.
This example is essentially a fold by sprint.
*)

(**
## Some syntax
The syntax follows F# as this is the language that the transformations domain specific language is written in.
This section is just a couple of things to keep in mind that might make becoming familiar with the
syntax needed to write transformations easier. The previous examples, together with this section should enable you
to not struggle too much with syntax errors.


When a command takes more than one argument, and these arguments aren't simple arguments (ie. contains a space), you will need
to use parenthesis around the arguments. 


When writing lists there are two ways to do this. They are both shown below.
*)
["Arg1"; "Arg2"; "Arg3"]
["Arg1"
 "Arg2"
 "Arg3"
]
(**
## Writing transformations and configurations
Say that you want to write a transformation that renames a column, and slices the columns. The two commands would look as follows:
*)
rename State.Name "NewName"

slice columns ["NewName"; ChangedDate.Name]
(**
To make this into a named transformation, that could be used in a configuration, one would have to write it as follows:
*)
let renameAndSlice =
    [
        rename State.Name "NewName"
        slice columns ["NewName"; ChangedDate.Name]
    ] |> createTransformation "renameAndSlice"
(**
A couple of things to notice in this example. First of all, you write it as a let statement to save it in a variable, in this
case named 'renameAndSlice', that you can then reference later when you need it for a configuration. Next is that you write
each transformation in a list, and finally you feed that list into a function called 'createTransformation' that also
takes a string, which will be the name of the transformation (Usually the let variable and this name should be the same.). This
has then created a transformation of these two commands, that can be used later on in a configuration.

Let's take a look at how to create a configuration.
*)

(*** hide ***)
let anotherTransformation =
    [
        rename State.Name "..."
    ] |> createTransformation "anotherTranformation"
let add _ _ = () 
(***)
[
    renameAndSlice
    anotherTransformation
] |> add "firstConfiguration"
(**
Much like how to write a transformation we simply create a list of the transformations, and then feed this to the
add function, which also takes the name that should be given to the configuration.
*)