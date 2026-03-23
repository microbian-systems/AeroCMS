"use strict";
const helloWorld = (name) => {
    console.log('hello ' + name + '!');
};
class MyTestClass {
    field1;
    constructor() {
        this.field1 = "Hello"; // Assigned here
    }
}
class Calculator {
    // We initialize the value to 0 immediately to avoid initialization errors
    currentResult = 0;
    constructor(initialValue = 0) {
        this.currentResult = initialValue;
    }
    add(value) {
        this.currentResult += value;
        return this; // Returning 'this' allows for method chaining
    }
    subtract(value) {
        this.currentResult -= value;
        return this;
    }
    multiply(value) {
        this.currentResult *= value;
        return this;
    }
    divide(value) {
        if (value === 0) {
            throw new Error("Cannot divide by zero.");
        }
        this.currentResult /= value;
        return this;
    }
    getResult() {
        return this.currentResult;
    }
    clear() {
        this.currentResult = 0;
        return this;
    }
}
// Usage Example:
const myCalc = new Calculator(10);
const finalValue = myCalc.add(5).multiply(2).subtract(4).getResult();
console.log(finalValue); // Output: 26
helloWorld('you');
