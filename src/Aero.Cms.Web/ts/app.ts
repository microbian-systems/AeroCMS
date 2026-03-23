

const helloWorld = (name: string): void => {
    console.log('hello ' + name + '!');
};

class MyTestClass {
    public field1: string;

    constructor() {
        this.field1 = "Hello"; // Assigned here
    }
}


class Calculator {
    // We initialize the value to 0 immediately to avoid initialization errors
    private currentResult: number = 0;

    constructor(initialValue: number = 0) {
        this.currentResult = initialValue;
    }

    public add(value: number): this {
        this.currentResult += value;
        return this; // Returning 'this' allows for method chaining
    }

    public subtract(value: number): this {
        this.currentResult -= value;
        return this;
    }

    public multiply(value: number): this {
        this.currentResult *= value;
        return this;
    }

    public divide(value: number): this {
        if (value === 0) {
            throw new Error("Cannot divide by zero.");
        }
        this.currentResult /= value;
        return this;
    }

    public getResult(): number {
        return this.currentResult;
    }

    public clear(): this {
        this.currentResult = 0;
        return this;
    }
}

// Usage Example:
const myCalc = new Calculator(10);
const finalValue = myCalc.add(5).multiply(2).subtract(4).getResult();

console.log(finalValue); // Output: 26

helloWorld('you');